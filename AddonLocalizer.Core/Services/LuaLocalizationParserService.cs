using System.Text.RegularExpressions;
using AddonLocalizer.Core.Interfaces;
using AddonLocalizer.Core.Models;

namespace AddonLocalizer.Core.Services;

public class LuaLocalizationParserService(IFileSystemService fileSystem) : ILuaLocalizationParserService
{
    private static readonly Regex LocalizationPattern = new(@"L\[""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex ConcatenationInsideBracketsPattern = new(@"L\[[^\]]*\.\.[^\]]*\]", RegexOptions.Compiled);
    private static readonly Regex AssignmentPattern = new(@"^\s*L\[""([^""]+)""\]\s*=", RegexOptions.Compiled);
    private static readonly Regex StringFormatFirstParamPattern = new(@"string\.format\s*\(\s*L\[""([^""]+)""\]", RegexOptions.Compiled);
    private static readonly Regex FormatSpecifierPattern = new (@"%(?:(\d+)\$)?([+-0#]*)(\d*)(?:\.(\d+))?([sdioxXeEfgGcuaA%])", RegexOptions.Compiled);
    
    private const int MaxLocationsPerString = 100;

    public LuaLocalizationParserService() : this(new FileSystemService())
    {
    }

    public async Task<ParseResult> ParseDirectoryAsync(string directoryPath, string[]? excludeSubdirectories = null)
    {
        if (!fileSystem.DirectoryExists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var result = new ParseResult();
        var luaFiles = fileSystem.GetFiles(directoryPath, "*.lua", SearchOption.AllDirectories);
        var filteredFiles = FilterExcludedPaths(luaFiles, directoryPath, excludeSubdirectories);

        foreach (var filePath in filteredFiles)
        {
            var fileResult = await ParseFileAsync(filePath);
            MergeResults(result, fileResult);
        }

        return result;
    }

    public async Task<ParseResult> ParseFileAsync(string filePath)
    {
        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var result = new ParseResult();
        var lines = await fileSystem.ReadAllLinesAsync(filePath);
        ProcessLines(lines, filePath, result);
        return result;
    }

    public async Task<HashSet<string>> ParseLocalizationDefinitionsAsync(string filePath)
    {
        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var definedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = await fileSystem.ReadAllLinesAsync(filePath);

        foreach (var line in lines)
        {
            var match = AssignmentPattern.Match(line);
            if (match is { Success: true, Groups.Count: > 1 })
            {
                var key = match.Groups[1].Value;
                definedKeys.Add(key);
            }
        }

        return definedKeys;
    }

    public async Task<HashSet<string>> ParseLocalizationUsagesAsync(string filePath)
    {
        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = await fileSystem.ReadAllLinesAsync(filePath);

        foreach (var line in lines)
        {
            var assignmentMatch = AssignmentPattern.Match(line);
            if (assignmentMatch.Success)
            {
                var assignmentIndex = line.IndexOf('=');
                if (assignmentIndex >= 0 && assignmentIndex < line.Length - 1)
                {
                    var rightSide = line[(assignmentIndex + 1)..];
                    var matches = LocalizationPattern.Matches(rightSide);
                    foreach (Match match in matches)
                    {
                        if (match.Success && match.Groups.Count > 1)
                        {
                            var key = match.Groups[1].Value;
                            usedKeys.Add(key);
                        }
                    }
                }
            }
        }

        return usedKeys;
    }

    public async Task<Dictionary<string, List<FormatParameter>>> ParseFormatParametersAsync(string filePath)
    {
        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var formatParameters = new Dictionary<string, List<FormatParameter>>(StringComparer.OrdinalIgnoreCase);
        var lines = await fileSystem.ReadAllLinesAsync(filePath);

        foreach (var line in lines)
        {
            var match = AssignmentPattern.Match(line);
            if (match is { Success: true, Groups.Count: > 1 })
            {
                var key = match.Groups[1].Value;
                var assignmentIndex = line.IndexOf('=');
                if (assignmentIndex >= 0 && assignmentIndex < line.Length - 1)
                {
                    var rightSide = line[(assignmentIndex + 1)..].Trim();
                    var value = ExtractStringValue(rightSide);
                    
                    if (value != null)
                    {
                        var parameters = ParseFormatSpecifiers(value);
                        if (parameters.Count > 0)
                        {
                            formatParameters[key] = parameters;
                        }
                    }
                }
            }
        }

        return formatParameters;
    }

    private static List<FormatParameter> ParseFormatSpecifiers(string formatString)
    {
        var parameters = new List<FormatParameter>();
        var matches = FormatSpecifierPattern.Matches(formatString);
        var position = 1;
        
        foreach (Match match in matches)
        {
            var specifierChar = match.Groups[5].Value[0];
            var hasPositionalIndex = !string.IsNullOrWhiteSpace(match.Groups[1].Value);
            var positionalIndex = hasPositionalIndex ? int.Parse(match.Groups[1].Value) : 0;
            
            int? width = !string.IsNullOrWhiteSpace(match.Groups[3].Value) ? int.Parse(match.Groups[3].Value) : null;
            int? precision = !string.IsNullOrWhiteSpace(match.Groups[4].Value) ? int.Parse(match.Groups[4].Value) : null;
            
            var paramType = specifierChar switch
            {
                's' or 'a' or 'A' => FormatParameterType.String,
                'd' or 'i' => FormatParameterType.Integer,
                'f' => FormatParameterType.Float,
                'c' => FormatParameterType.Character,
                'u' => FormatParameterType.Unsigned,
                'x' or 'X' => FormatParameterType.Hexadecimal,
                'o' => FormatParameterType.Octal,
                'e' or 'E' => FormatParameterType.Exponential,
                'g' or 'G' => FormatParameterType.General,
                '%' => FormatParameterType.Percent,
                _ => FormatParameterType.String
            };
            
            parameters.Add(new FormatParameter
            {
                Position = hasPositionalIndex ? positionalIndex : position,
                Type = paramType,
                RawSpecifier = match.Value,
                Width = width,
                Precision = precision,
                HasPositionalIndex = hasPositionalIndex
            });
            
            if (!hasPositionalIndex && paramType != FormatParameterType.Percent)
            {
                position++;
            }
        }
        
        return parameters;
    }

    public HashSet<string> ParseLocalizationDefinitions(string filePath)
    {
        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var definedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = fileSystem.ReadAllLines(filePath);

        foreach (var line in lines)
        {
            var match = AssignmentPattern.Match(line);
            if (match is { Success: true, Groups.Count: > 1 })
            {
                var key = match.Groups[1].Value;
                definedKeys.Add(key);
            }
        }

        return definedKeys;
    }

    public HashSet<string> ParseLocalizationUsages(string filePath)
    {
        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = fileSystem.ReadAllLines(filePath);

        foreach (var line in lines)
        {
            var assignmentMatch = AssignmentPattern.Match(line);
            if (assignmentMatch.Success)
            {
                var assignmentIndex = line.IndexOf('=');
                if (assignmentIndex >= 0 && assignmentIndex < line.Length - 1)
                {
                    var rightSide = line[(assignmentIndex + 1)..];
                    var matches = LocalizationPattern.Matches(rightSide);
                    foreach (Match match in matches)
                    {
                        if (match.Success && match.Groups.Count > 1)
                        {
                            var key = match.Groups[1].Value;
                            usedKeys.Add(key);
                        }
                    }
                }
            }
        }

        return usedKeys;
    }

    public ParseResult ParseDirectory(string directoryPath, string[]? excludeSubdirectories = null)
    {
        if (!fileSystem.DirectoryExists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        var result = new ParseResult();
        var luaFiles = fileSystem.GetFiles(directoryPath, "*.lua", SearchOption.AllDirectories);
        var filteredFiles = FilterExcludedPaths(luaFiles, directoryPath, excludeSubdirectories);

        foreach (var filePath in filteredFiles)
        {
            var fileResult = ParseFile(filePath);
            MergeResults(result, fileResult);
        }

        return result;
    }

    public ParseResult ParseFile(string filePath)
    {
        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var result = new ParseResult();
        var lines = fileSystem.ReadAllLines(filePath);
        ProcessLines(lines, filePath, result);
        return result;
    }

    /// <summary>
    /// Parses a locale-specific file to extract translations
    /// </summary>
    /// <param name="filePath">Path to locale file (e.g., Localization/deDE.lua)</param>
    /// <returns>Dictionary of glue string -> translated value</returns>
    public async Task<Dictionary<string, string>> ParseLocaleTranslationsAsync(string filePath)
    {
        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = await fileSystem.ReadAllLinesAsync(filePath);

        foreach (var line in lines)
        {
            var match = AssignmentPattern.Match(line);
            if (match is { Success: true, Groups.Count: > 1 })
            {
                var key = match.Groups[1].Value;
                var assignmentIndex = line.IndexOf('=');
                
                if (assignmentIndex >= 0 && assignmentIndex < line.Length - 1)
                {
                    var rightSide = line[(assignmentIndex + 1)..].Trim();
                    var value = ExtractStringValue(rightSide);
                    
                    if (value != null)
                    {
                        translations[key] = value;
                    }
                }
            }
        }

        return translations;
    }

    /// <summary>
    /// Parses entire Localization directory, loading all locale files
    /// </summary>
    /// <param name="localizationDir">Path to Localization directory</param>
    /// <param name="excludePatterns">Patterns to exclude (e.g., ["*GT.lua"])</param>
    /// <returns>Complete localization dataset</returns>
    public async Task<LocalizationDataSet> ParseLocalizationDirectoryAsync(
        string localizationDir, 
        string[]? excludePatterns = null)
    {
        if (!fileSystem.DirectoryExists(localizationDir))
        {
            throw new DirectoryNotFoundException($"Directory not found: {localizationDir}");
        }

        var dataSet = new LocalizationDataSet();
        var luaFiles = fileSystem.GetFiles(localizationDir, "*.lua", SearchOption.TopDirectoryOnly);
        
        // Exclude GT (Google Translate) files by default
        excludePatterns ??= ["GT.lua"];
        var filteredFiles = luaFiles.Where(f => 
            !excludePatterns.Any(pattern => 
                Path.GetFileName(f).EndsWith(pattern.TrimStart('*'), 
                    StringComparison.OrdinalIgnoreCase))
        ).ToArray();

        foreach (var filePath in filteredFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            
            // Check if this is a valid locale code
            if (LocaleDefinitions.IsValidLocale(fileName))
            {
                var translations = await ParseLocaleTranslationsAsync(filePath);
                dataSet.AddLocale(fileName, translations);
            }
        }

        return dataSet;
    }

    /// <summary>
    /// Synchronous version of ParseLocaleTranslationsAsync
    /// </summary>
    public Dictionary<string, string> ParseLocaleTranslations(string filePath)
    {
        if (!fileSystem.FileExists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = fileSystem.ReadAllLines(filePath);

        foreach (var line in lines)
        {
            var match = AssignmentPattern.Match(line);
            if (match is { Success: true, Groups.Count: > 1 })
            {
                var key = match.Groups[1].Value;
                var assignmentIndex = line.IndexOf('=');
                
                if (assignmentIndex >= 0 && assignmentIndex < line.Length - 1)
                {
                    var rightSide = line[(assignmentIndex + 1)..].Trim();
                    var value = ExtractStringValue(rightSide);
                    
                    if (value != null)
                    {
                        translations[key] = value;
                    }
                }
            }
        }

        return translations;
    }

    private static string? ExtractStringValue(string rightSide)
    {
        // Remove trailing comments and whitespace
        var commentIndex = rightSide.IndexOf("--");
        if (commentIndex >= 0)
        {
            rightSide = rightSide[..commentIndex].Trim();
        }
        
        // Handle double quotes
        if (rightSide.StartsWith('"'))
        {
            var endQuote = FindClosingQuote(rightSide, '"', 1);
            if (endQuote > 0)
            {
                return rightSide[1..endQuote];
            }
        }
        
        // Handle single quotes
        if (rightSide.StartsWith('\''))
        {
            var endQuote = FindClosingQuote(rightSide, '\'', 1);
            if (endQuote > 0)
            {
                return rightSide[1..endQuote];
            }
        }
        
        return null;
    }

    private static int FindClosingQuote(string text, char quoteChar, int startIndex)
    {
        for (int i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++;
                continue;
            }
            if (text[i] == quoteChar)
            {
                return i;
            }
        }
        return -1;
    }

    private static void ProcessLines(string[] lines, string filePath, ParseResult result)
    {
        // Track which glue strings appear in this file
        var stringsInThisFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;
            
            var formatMatches = StringFormatFirstParamPattern.Matches(line);
            var keysInStringFormat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (Match formatMatch in formatMatches)
            {
                if (formatMatch.Success && formatMatch.Groups.Count > 1)
                {
                    keysInStringFormat.Add(formatMatch.Groups[1].Value);
                }
            }
            
            var lineConcatenationMatches = ConcatenationInsideBracketsPattern.Matches(line);
            var concatenatedKeysOnLine = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (Match concatMatch in lineConcatenationMatches)
            {
                var concatText = concatMatch.Value;
                var keyMatch = Regex.Match(concatText, @"L\[""([^""]+)""");
                if (keyMatch.Success)
                {
                    concatenatedKeysOnLine.Add(keyMatch.Groups[1].Value);
                }
            }
            
            var matches = LocalizationPattern.Matches(line);

            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    var glueString = match.Groups[1].Value;
                    var isInStringFormat = keysInStringFormat.Contains(glueString);
                    var isConcatenated = concatenatedKeysOnLine.Contains(glueString);

                    if (!result.GlueStrings.TryGetValue(glueString, out var info))
                    {
                        info = new GlueStringInfo
                        {
                            GlueString = glueString,
                            HasConcatenation = isConcatenated,
                            UsedInStringFormat = isInStringFormat
                        };
                        result.GlueStrings[glueString] = info;
                    }
                    else
                    {
                        if (isConcatenated) info.HasConcatenation = true;
                        if (isInStringFormat) info.UsedInStringFormat = true;
                    }

                    info.OccurrenceCount++;
                    
                    // Track that this string appears in this file
                    stringsInThisFile.Add(glueString);

                    // Store detailed line locations ONLY for problematic cases
                    if (isConcatenated && info.Locations.Count < MaxLocationsPerString)
                    {
                        info.Locations.Add(new GlueStringLocation
                        {
                            FilePath = filePath,
                            LineNumber = lineNumber
                        });
                    }
                    
                    if (isInStringFormat && info.StringFormatLocations.Count < MaxLocationsPerString)
                    {
                        info.StringFormatLocations.Add(new GlueStringLocation
                        {
                            FilePath = filePath,
                            LineNumber = lineNumber
                        });
                    }
                }
            }
        }
        
        // After processing all lines, ensure each glue string has a file reference
        // This populates the Files column even for non-problematic strings
        foreach (var glueString in stringsInThisFile)
        {
            var info = result.GlueStrings[glueString];
            
            // Check if this file is already in Locations
            bool fileAlreadyTracked = info.Locations.Any(l => 
                l.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            
            // If not tracked yet and we have room, add a file-only reference
            if (!fileAlreadyTracked && info.Locations.Count < MaxLocationsPerString)
            {
                info.Locations.Add(new GlueStringLocation
                {
                    FilePath = filePath,
                    LineNumber = 0  // 0 = file reference only, no specific line
                });
            }
        }
    }

    private static void MergeResults(ParseResult target, ParseResult source)
    {
        foreach (var (glueString, sourceInfo) in source.GlueStrings)
        {
            if (!target.GlueStrings.TryGetValue(glueString, out var targetInfo))
            {
                target.GlueStrings[glueString] = sourceInfo;
            }
            else
            {
                if (sourceInfo.HasConcatenation) targetInfo.HasConcatenation = true;
                if (sourceInfo.UsedInStringFormat) targetInfo.UsedInStringFormat = true;
                
                targetInfo.OccurrenceCount += sourceInfo.OccurrenceCount;

                if (targetInfo.Locations.Count < MaxLocationsPerString)
                {
                    var toAdd = Math.Min(
                        sourceInfo.Locations.Count,
                        MaxLocationsPerString - targetInfo.Locations.Count
                    );
                    targetInfo.Locations.AddRange(sourceInfo.Locations.Take(toAdd));
                }
                
                if (targetInfo.StringFormatLocations.Count < MaxLocationsPerString)
                {
                    var toAdd = Math.Min(
                        sourceInfo.StringFormatLocations.Count,
                        MaxLocationsPerString - targetInfo.StringFormatLocations.Count
                    );
                    targetInfo.StringFormatLocations.AddRange(sourceInfo.StringFormatLocations.Take(toAdd));
                }
            }
        }
    }

    private static string[] FilterExcludedPaths(string[] filePaths, string baseDirectory, string[]? excludeSubdirectories)
    {
        if (excludeSubdirectories == null || excludeSubdirectories.Length == 0)
        {
            return filePaths;
        }

        return filePaths.Where(filePath =>
        {
            var normalizedFilePath = filePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            
            foreach (var excludedSubdirectory in excludeSubdirectories)
            {
                var normalizedExclude = excludedSubdirectory.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                var pathSegments = normalizedFilePath.Split(Path.DirectorySeparatorChar);
                var excludeSegments = normalizedExclude.Split(Path.DirectorySeparatorChar);
                
                for (int i = 0; i <= pathSegments.Length - excludeSegments.Length; i++)
                {
                    bool matches = true;
                    for (int j = 0; j < excludeSegments.Length; j++)
                    {
                        if (!pathSegments[i + j].Equals(excludeSegments[j], StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                            break;
                        }
                    }
                    
                    if (matches)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }).ToArray();
    }

    private string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}