using System.Text;
using AddonLocalizer.Core.Interfaces;
using AddonLocalizer.Core.Models;

namespace AddonLocalizer.Core.Services;

/// <summary>
/// Service for writing localization data back to Lua files
/// </summary>
public class LocalizationFileWriterService : ILocalizationFileWriterService
{
    private readonly IFileSystemService _fileSystem;
    private const string BackupExtension = ".backup";

    public LocalizationFileWriterService(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public LocalizationFileWriterService() : this(new FileSystemService())
    {
    }

    /// <summary>
    /// Save translations for a specific locale to its file
    /// </summary>
    /// <param name="localizationDir">Path to Localization directory</param>
    /// <param name="localeCode">Locale code (e.g., "deDE")</param>
    /// <param name="translations">Dictionary of glue string -> translation</param>
    /// <param name="createBackup">Whether to create a backup before writing</param>
    public async Task SaveLocaleFileAsync(
        string localizationDir, 
        string localeCode, 
        Dictionary<string, string> translations,
        bool createBackup = true)
    {
        if (!_fileSystem.DirectoryExists(localizationDir))
        {
            throw new DirectoryNotFoundException($"Localization directory not found: {localizationDir}");
        }

        if (!LocaleDefinitions.IsValidLocale(localeCode))
        {
            throw new ArgumentException($"Invalid locale code: {localeCode}", nameof(localeCode));
        }

        var filePath = Path.Combine(localizationDir, $"{localeCode}.lua");

        // Create backup if file exists and backup is requested
        if (createBackup && _fileSystem.FileExists(filePath))
        {
            await CreateBackupAsync(filePath);
        }

        // Read existing file structure if it exists
        List<string>? existingLines = null;
        if (_fileSystem.FileExists(filePath))
        {
            existingLines = (await _fileSystem.ReadAllLinesAsync(filePath)).ToList();
        }

        // Generate new file content
        var newContent = GenerateLocaleFileContent(localeCode, translations, existingLines);

        // Write to file
        await _fileSystem.WriteAllLinesAsync(filePath, newContent);
    }

    /// <summary>
    /// Save multiple locale files at once
    /// </summary>
    public async Task SaveMultipleLocaleFilesAsync(
        string localizationDir,
        Dictionary<string, Dictionary<string, string>> localeTranslations,
        bool createBackup = true,
        IProgress<SaveProgress>? progress = null)
    {
        if (!_fileSystem.DirectoryExists(localizationDir))
        {
            throw new DirectoryNotFoundException($"Localization directory not found: {localizationDir}");
        }

        var totalLocales = localeTranslations.Count;
        var processedLocales = 0;

        foreach (var (localeCode, translations) in localeTranslations)
        {
            try
            {
                await SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup);
                
                processedLocales++;
                progress?.Report(new SaveProgress
                {
                    LocaleCode = localeCode,
                    ProcessedCount = processedLocales,
                    TotalCount = totalLocales,
                    IsComplete = processedLocales == totalLocales
                });
            }
            catch (Exception ex)
            {
                progress?.Report(new SaveProgress
                {
                    LocaleCode = localeCode,
                    ProcessedCount = processedLocales,
                    TotalCount = totalLocales,
                    Error = ex.Message
                });
                throw;
            }
        }
    }

    /// <summary>
    /// Create a backup of the file with timestamp
    /// </summary>
    private async Task CreateBackupAsync(string filePath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = $"{filePath}.{timestamp}{BackupExtension}";
        
        var content = await _fileSystem.ReadAllTextAsync(filePath);
        await _fileSystem.WriteAllTextAsync(backupPath, content);
    }

    /// <summary>
    /// Generate the complete Lua file content
    /// </summary>
    private List<string> GenerateLocaleFileContent(
        string localeCode, 
        Dictionary<string, string> translations,
        List<string>? existingLines)
    {
        var lines = new List<string>();

        // If we have existing lines, try to preserve the structure
        if (existingLines != null && existingLines.Count > 0)
        {
            return MergeWithExistingFile(existingLines, translations);
        }

        // Generate new file from scratch
        lines.Add("local _, TRB = ...");
        lines.Add("");
        lines.Add($"local locale = GetLocale()");
        lines.Add("");
        lines.Add($"if locale == \"{localeCode}\" then");
        lines.Add("    local L = TRB.Localization");
        lines.Add("    ");

        // Sort translations by key for consistency
        var sortedTranslations = translations
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in sortedTranslations)
        {
            var escapedValue = EscapeString(value);
            lines.Add($"    L[\"{key}\"] = \"{escapedValue}\"");
        }

        lines.Add("end");
        lines.Add("");

        return lines;
    }

    /// <summary>
    /// Merge new translations with existing file structure
    /// </summary>
    private List<string> MergeWithExistingFile(
        List<string> existingLines,
        Dictionary<string, string> translations)
    {
        var result = new List<string>();
        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inLocaleBlock = false;
        var localeBlockStartIndex = -1;
        var localeBlockEndIndex = -1;

        // Find the locale block boundaries
        for (int i = 0; i < existingLines.Count; i++)
        {
            var line = existingLines[i].TrimStart();
            
            if (line.StartsWith("if locale =="))
            {
                inLocaleBlock = true;
                localeBlockStartIndex = i;
            }
            else if (inLocaleBlock && line.StartsWith("end"))
            {
                localeBlockEndIndex = i;
                break;
            }
        }

        // Copy everything before the locale block
        for (int i = 0; i <= localeBlockStartIndex; i++)
        {
            result.Add(existingLines[i]);
        }

        // Process locale block
        if (localeBlockStartIndex >= 0)
        {
            // Add local L = TRB.Localization line if exists
            for (int i = localeBlockStartIndex + 1; i < localeBlockEndIndex; i++)
            {
                var line = existingLines[i];
                
                // Check if this is an assignment line
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("L[\""))
                {
                    // Extract the key
                    var keyMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"L\[""([^""]+)""\]");
                    if (keyMatch.Success)
                    {
                        var key = keyMatch.Groups[1].Value;
                        
                        // Update with new translation if available
                        if (translations.TryGetValue(key, out var newValue) && !string.IsNullOrWhiteSpace(newValue))
                        {
                            var lineIndent = GetIndentation(line);
                            var escapedValue = EscapeString(newValue);
                            result.Add($"{lineIndent}L[\"{key}\"] = \"{escapedValue}\"");
                            processedKeys.Add(key);
                        }
                        else if (!translations.ContainsKey(key))
                        {
                            // Keep existing line if key not in new translations
                            result.Add(line);
                        }
                        // Skip if translation is empty string
                    }
                }
                else if (!trimmed.StartsWith("--") || trimmed.Contains("L["))
                {
                    // Keep non-assignment lines (like local L = TRB.Localization)
                    result.Add(line);
                }
            }

            // Add new translations that weren't in the original file
            var defaultIndent = "    ";
            foreach (var (key, value) in translations.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!processedKeys.Contains(key) && !string.IsNullOrWhiteSpace(value))
                {
                    var escapedValue = EscapeString(value);
                    result.Add($"{defaultIndent}L[\"{key}\"] = \"{escapedValue}\"");
                }
            }

            // Add the closing 'end'
            result.Add(existingLines[localeBlockEndIndex]);

            // Copy everything after the locale block
            for (int i = localeBlockEndIndex + 1; i < existingLines.Count; i++)
            {
                result.Add(existingLines[i]);
            }
        }
        else
        {
            // No locale block found, return original with warning comment
            result.Add("-- Warning: Could not merge translations, preserving original file");
            result.AddRange(existingLines);
        }

        return result;
    }

    /// <summary>
    /// Get the indentation from a line
    /// </summary>
    private static string GetIndentation(string line)
    {
        var indent = 0;
        foreach (var ch in line)
        {
            if (ch == ' ')
                indent++;
            else if (ch == '\t')
                indent += 4; // Treat tab as 4 spaces
            else
                break;
        }
        return new string(' ', indent);
    }

    /// <summary>
    /// Escape special characters in strings for Lua
    /// </summary>
    private static string EscapeString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value
            .Replace("\\", "\\\\")  // Backslash must be first
            .Replace("\"", "\\\"")  // Escape quotes
            .Replace("\n", "\\n")   // Newline
            .Replace("\r", "\\r")   // Carriage return
            .Replace("\t", "\\t");  // Tab
    }

    /// <summary>
    /// Delete all backup files for a locale
    /// </summary>
    public Task DeleteBackupsAsync(string localizationDir, string localeCode)
    {
        var filePath = Path.Combine(localizationDir, $"{localeCode}.lua");
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (directory != null && _fileSystem.DirectoryExists(directory))
        {
            var backupPattern = $"{fileName}.*{BackupExtension}";
            var backupFiles = _fileSystem.GetFiles(directory, backupPattern, SearchOption.TopDirectoryOnly);

            foreach (var backup in backupFiles)
            {
                File.Delete(backup);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Restore from the most recent backup
    /// </summary>
    public async Task RestoreFromBackupAsync(string localizationDir, string localeCode)
    {
        var filePath = Path.Combine(localizationDir, $"{localeCode}.lua");
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (directory == null || !_fileSystem.DirectoryExists(directory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory}");
        }

        var backupPattern = $"{fileName}.*{BackupExtension}";
        var backupFiles = _fileSystem.GetFiles(directory, backupPattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => f)
            .ToArray();

        if (backupFiles.Length == 0)
        {
            throw new FileNotFoundException($"No backup found for {localeCode}.lua");
        }

        var mostRecentBackup = backupFiles[0];
        var content = await _fileSystem.ReadAllTextAsync(mostRecentBackup);
        await _fileSystem.WriteAllTextAsync(filePath, content);
    }
}

/// <summary>
/// Progress information for multi-file save operations
/// </summary>
public class SaveProgress
{
    public string LocaleCode { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public bool IsComplete { get; set; }
    public string? Error { get; set; }
    
    public double PercentComplete => TotalCount > 0 ? (ProcessedCount / (double)TotalCount) * 100 : 0;
}
