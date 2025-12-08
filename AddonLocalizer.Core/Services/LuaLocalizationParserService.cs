using System.Text.RegularExpressions;
using AddonLocalizer.Core.Interfaces;
using AddonLocalizer.Core.Models;

namespace AddonLocalizer.Core.Services;

public class LuaLocalizationParserService(IFileSystemService fileSystem) : ILuaLocalizationParserService
{
    // Matches L["string"] pattern - now captures strings even if followed by concatenation
    private static readonly Regex LocalizationPattern = new(@"L\[""([^""]+)""", RegexOptions.Compiled);
    // Matches L[anything .. anything] pattern - concatenation INSIDE the brackets
    private static readonly Regex ConcatenationInsideBracketsPattern = new(@"L\[[^\]]*\.\.[^\]]*\]", RegexOptions.Compiled);
    // Matches L["key"] = assignment pattern (for parsing localization definition files)
    private static readonly Regex AssignmentPattern = new(@"^\s*L\[""([^""]+)""\]\s*=", RegexOptions.Compiled);

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

        // Filter out excluded subdirectories
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
            // Only match L["key"] = ... patterns (assignment)
            var match = AssignmentPattern.Match(line);
            if (match.Success && match.Groups.Count > 1)
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
            // Check if this is an assignment line
            var assignmentMatch = AssignmentPattern.Match(line);
            if (assignmentMatch.Success)
            {
                // Get the part after the = (the right side)
                var assignmentIndex = line.IndexOf('=');
                if (assignmentIndex >= 0 && assignmentIndex < line.Length - 1)
                {
                    var rightSide = line.Substring(assignmentIndex + 1);
                    
                    // Find all L["..."] patterns in the right side
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
            // Only match L["key"] = ... patterns (assignment)
            var match = AssignmentPattern.Match(line);
            if (match.Success && match.Groups.Count > 1)
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
            // Check if this is an assignment line
            var assignmentMatch = AssignmentPattern.Match(line);
            if (assignmentMatch.Success)
            {
                // Get the part after the = (the right side)
                var assignmentIndex = line.IndexOf('=');
                if (assignmentIndex >= 0 && assignmentIndex < line.Length - 1)
                {
                    var rightSide = line.Substring(assignmentIndex + 1);
                    
                    // Find all L["..."] patterns in the right side
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

        // Filter out excluded subdirectories
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

    private string[] FilterExcludedPaths(string[] filePaths, string baseDirectory, string[]? excludeSubdirectories)
    {
        if (excludeSubdirectories == null || excludeSubdirectories.Length == 0)
        {
            return filePaths;
        }

        return filePaths.Where(filePath =>
        {
            // Normalize the file path to use consistent separators
            var normalizedFilePath = filePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            
            // Check if the file path contains any of the excluded subdirectories
            foreach (var excludedSubdir in excludeSubdirectories)
            {
                var normalizedExclude = excludedSubdir.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                
                // Check if the path contains the excluded subdirectory as a complete path segment
                var pathSegments = normalizedFilePath.Split(Path.DirectorySeparatorChar);
                var excludeSegments = normalizedExclude.Split(Path.DirectorySeparatorChar);
                
                // Check if excludeSegments appear consecutively in pathSegments
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
                        return false; // Exclude this file
                    }
                }
            }
            
            return true; // Include this file
        }).ToArray();
    }

    private string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private void ProcessLines(string[] lines, string filePath, ParseResult result)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;
            
            // Check if this line has concatenation inside brackets: L["string" .. var]
            var hasConcatenationInsideBrackets = ConcatenationInsideBracketsPattern.IsMatch(line);
            
            var matches = LocalizationPattern.Matches(line);

            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var glueString = match.Groups[1].Value;

                        if (!result.GlueStrings.TryGetValue(glueString, out var info))
                        {
                            info = new GlueStringInfo
                            {
                                GlueString = glueString,
                                HasConcatenation = hasConcatenationInsideBrackets
                            };
                            result.GlueStrings[glueString] = info;
                        }

                        info.OccurrenceCount++;

                        if (hasConcatenationInsideBrackets)
                        {
                            info.Locations.Add(new GlueStringLocation
                            {
                                FilePath = filePath,
                                LineNumber = lineNumber
                            });
                        }
                    }
                }
            }
        }
    }

    private static void MergeResults(ParseResult target, ParseResult source)
    {
        foreach (var (glueString, sourceInfo) in source.GlueStrings)
        {
            if (!target.GlueStrings.TryGetValue(glueString, out var targetInfo))
            {
                targetInfo = new GlueStringInfo
                {
                    GlueString = glueString,
                    HasConcatenation = sourceInfo.HasConcatenation
                };
                target.GlueStrings[glueString] = targetInfo;
            }

            targetInfo.OccurrenceCount += sourceInfo.OccurrenceCount;

            if (sourceInfo.HasConcatenation)
            {
                targetInfo.Locations.AddRange(sourceInfo.Locations);
            }
        }
    }
}