using AddonLocalizer.Core.Interfaces;
using AddonLocalizer.Core.Models;

namespace AddonLocalizer.Core.Services;

/// <summary>
/// Service for writing localization data back to Lua files
/// </summary>
public class LocalizationFileWriterService(IFileSystemService fileSystem) : ILocalizationFileWriterService
{
    private const string BackupExtension = ".backup";

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
        System.Diagnostics.Debug.WriteLine($"[FileWriter] SaveLocaleFileAsync called for {localeCode}");
        System.Diagnostics.Debug.WriteLine($"[FileWriter] Directory: {localizationDir}");
        System.Diagnostics.Debug.WriteLine($"[FileWriter] Translations count: {translations.Count}");
        
        if (!fileSystem.DirectoryExists(localizationDir))
        {
            throw new DirectoryNotFoundException($"Localization directory not found: {localizationDir}");
        }

        if (!LocaleDefinitions.IsValidLocale(localeCode))
        {
            throw new ArgumentException($"Invalid locale code: {localeCode}", nameof(localeCode));
        }

        var filePath = Path.Combine(localizationDir, $"{localeCode}.lua");
        System.Diagnostics.Debug.WriteLine($"[FileWriter] Target file path: {filePath}");
        System.Diagnostics.Debug.WriteLine($"[FileWriter] File exists: {fileSystem.FileExists(filePath)}");

        // Create backup if file exists and backup is requested
        if (createBackup && fileSystem.FileExists(filePath))
        {
            System.Diagnostics.Debug.WriteLine($"[FileWriter] Creating backup...");
            await CreateBackupAsync(filePath);
        }

        // Read existing file structure if it exists
        List<string>? existingLines = null;
        if (fileSystem.FileExists(filePath))
        {
            existingLines = (await fileSystem.ReadAllLinesAsync(filePath)).ToList();
            System.Diagnostics.Debug.WriteLine($"[FileWriter] Read {existingLines.Count} existing lines from file");
        }

        // Generate new file content
        var newContent = GenerateLocaleFileContent(localeCode, translations, existingLines);
        System.Diagnostics.Debug.WriteLine($"[FileWriter] Generated {newContent.Count} new lines");

        // Write to file
        System.Diagnostics.Debug.WriteLine($"[FileWriter] Writing to file...");
        await fileSystem.WriteAllLinesAsync(filePath, newContent);
        System.Diagnostics.Debug.WriteLine($"[FileWriter] File written successfully");
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
        if (!fileSystem.DirectoryExists(localizationDir))
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
        
        var content = await fileSystem.ReadAllTextAsync(filePath);
        await fileSystem.WriteAllTextAsync(backupPath, content);
    }

    /// <summary>
    /// Generate the complete Lua file content
    /// </summary>
    private static List<string> GenerateLocaleFileContent(
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
            // Escape the value for Lua output (handles actual newlines, etc.)
            lines.Add($"    L[\"{key}\"] = \"{EscapeLuaString(value)}\"");
        }

        lines.Add("end");
        lines.Add("");

        return lines;
    }

    /// <summary>
    /// Merge new translations with existing file structure
    /// </summary>
    private static List<string> MergeWithExistingFile(
        List<string> existingLines,
        Dictionary<string, string> translations)
    {
        var result = new List<string>();
        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localeBlockStartIndex = -1;
        var localeBlockEndIndex = -1;
        var hasLocaleBlock = false;

        System.Diagnostics.Debug.WriteLine($"[FileWriter] MergeWithExistingFile: {existingLines.Count} existing lines, {translations.Count} translations");

        // Find the locale block boundaries (if present)
        for (var i = 0; i < existingLines.Count; i++)
        {
            var line = existingLines[i].TrimStart();
            
            if (line.StartsWith("if locale ==") || line.StartsWith("if locale=="))
            {
                hasLocaleBlock = true;
                localeBlockStartIndex = i;
            }
            else if (hasLocaleBlock && localeBlockEndIndex == -1 && line.StartsWith("end"))
            {
                localeBlockEndIndex = i;
                break;
            }
        }

        System.Diagnostics.Debug.WriteLine($"[FileWriter] Has locale block: {hasLocaleBlock}, start: {localeBlockStartIndex}, end: {localeBlockEndIndex}");

        // If no locale block, find the range of L["key"] assignments
        if (!hasLocaleBlock)
        {
            var firstAssignmentIndex = -1;
            var lastAssignmentIndex = -1;
            
            for (var i = 0; i < existingLines.Count; i++)
            {
                var trimmed = existingLines[i].TrimStart();
                if (trimmed.StartsWith("L[\""))
                {
                    if (firstAssignmentIndex == -1)
                    {
                        firstAssignmentIndex = i;
                    }
                    lastAssignmentIndex = i;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[FileWriter] Assignment range: {firstAssignmentIndex} to {lastAssignmentIndex}");

            if (firstAssignmentIndex >= 0)
            {
                // Copy everything before the first assignment
                for (var i = 0; i < firstAssignmentIndex; i++)
                {
                    result.Add(existingLines[i]);
                }

                var skippedDuplicates = 0;
                var skippedOrphans = 0;
                var written = 0;

                // Process existing assignments
                for (var i = firstAssignmentIndex; i <= lastAssignmentIndex; i++)
                {
                    var line = existingLines[i];
                    var trimmed = line.TrimStart();
                    
                    if (trimmed.StartsWith("L[\""))
                    {
                        var keyMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"L\[""([^""]+)""\]");
                        if (keyMatch.Success)
                        {
                            var key = keyMatch.Groups[1].Value;
                            
                            // Only include this key if it's in the translations dictionary AND not already processed
                            // This handles duplicates by keeping only the first occurrence
                            if (processedKeys.Contains(key))
                            {
                                skippedDuplicates++;
                                System.Diagnostics.Debug.WriteLine($"[FileWriter] Skipping duplicate: {key}");
                            }
                            else if (translations.TryGetValue(key, out var newValue))
                            {
                                if (!string.IsNullOrWhiteSpace(newValue))
                                {
                                    var lineIndent = GetIndentation(line);
                                    result.Add($"{lineIndent}L[\"{key}\"] = \"{EscapeLuaString(newValue)}\"");
                                    processedKeys.Add(key);
                                    written++;
                                }
                            }
                            else
                            {
                                skippedOrphans++;
                                System.Diagnostics.Debug.WriteLine($"[FileWriter] Skipping orphan: {key}");
                            }
                            // If key already processed or not in translations, skip it (removes duplicates and orphaned entries)
                        }
                    }
                    else
                    {
                        // Keep non-assignment lines (comments, blank lines, etc.)
                        result.Add(line);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[FileWriter] Processed: written={written}, skippedDuplicates={skippedDuplicates}, skippedOrphans={skippedOrphans}");

                // Add new translations that weren't in the original file
                var defaultIndent = firstAssignmentIndex > 0 
                    ? GetIndentation(existingLines[firstAssignmentIndex]) 
                    : "";
                
                var newKeys = 0;
                foreach (var (key, value) in translations.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    if (!processedKeys.Contains(key) && !string.IsNullOrWhiteSpace(value))
                    {
                        result.Add($"{defaultIndent}L[\"{key}\"] = \"{EscapeLuaString(value)}\"");
                        newKeys++;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[FileWriter] Added {newKeys} new keys");

                // Copy everything after the last assignment
                for (var i = lastAssignmentIndex + 1; i < existingLines.Count; i++)
                {
                    result.Add(existingLines[i]);
                }

                System.Diagnostics.Debug.WriteLine($"[FileWriter] Final result: {result.Count} lines");
                return result;
            }
            else
            {
                // No assignments found at all, return original with warning
                System.Diagnostics.Debug.WriteLine($"[FileWriter] No assignments found, preserving original file");
                result.Add("-- Warning: Could not find any L[\"key\"] assignments, preserving original file");
                result.AddRange(existingLines);
                return result;
            }
        }

        // Has locale block - use original logic
        // Copy everything before the locale block (including the if line)
        for (var i = 0; i <= localeBlockStartIndex; i++)
        {
            result.Add(existingLines[i]);
        }

        // Process locale block content
        for (var i = localeBlockStartIndex + 1; i < localeBlockEndIndex; i++)
        {
            var line = existingLines[i];
            var trimmed = line.TrimStart();
            
            if (trimmed.StartsWith("L[\""))
            {
                var keyMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"L\[""([^""]+)""\]");
                if (keyMatch.Success)
                {
                    var key = keyMatch.Groups[1].Value;
                    
                    // Only include this key if it's in the translations dictionary AND not already processed
                    // This handles duplicates by keeping only the first occurrence
                    if (!processedKeys.Contains(key) && translations.TryGetValue(key, out var newValue))
                    {
                        if (!string.IsNullOrWhiteSpace(newValue))
                        {
                            var lineIndent = GetIndentation(line);
                            result.Add($"{lineIndent}L[\"{key}\"] = \"{EscapeLuaString(newValue)}\"");
                            processedKeys.Add(key);
                        }
                    }
                    // If key already processed or not in translations, skip it (removes duplicates and orphaned entries)
                }
            }
            else if (!trimmed.StartsWith("--") || trimmed.Contains("L["))
            {
                // Keep non-assignment lines (like local L = TRB.Localization)
                result.Add(line);
            }
        }

        // Add new translations that weren't in the original file
        const string defaultBlockIndent = "    ";
        foreach (var (key, value) in translations.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!processedKeys.Contains(key) && !string.IsNullOrWhiteSpace(value))
            {
                result.Add($"{defaultBlockIndent}L[\"{key}\"] = \"{EscapeLuaString(value)}\"");
            }
        }

        // Add the closing 'end'
        result.Add(existingLines[localeBlockEndIndex]);

        // Copy everything after the locale block
        for (var i = localeBlockEndIndex + 1; i < existingLines.Count; i++)
        {
            result.Add(existingLines[i]);
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
    /// Escape special characters in strings for Lua output.
    /// This handles ACTUAL special characters (newlines, tabs, etc.) that need to be 
    /// represented as escape sequences in the Lua file.
    /// It does NOT double-escape already-escaped sequences.
    /// </summary>
    private static string EscapeLuaString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sb = new System.Text.StringBuilder(value.Length);
        
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            
            switch (c)
            {
                case '"':
                    // Check if already escaped (previous char is backslash and that backslash is not itself escaped)
                    if (i > 0 && value[i - 1] == '\\' && (i < 2 || value[i - 2] != '\\'))
                    {
                        sb.Append(c); // Already escaped, keep as-is
                    }
                    else
                    {
                        sb.Append("\\\"");
                    }
                    break;
                    
                case '\\':
                    // Check if this backslash is an escape sequence prefix
                    if (i + 1 < value.Length)
                    {
                        char next = value[i + 1];
                        if (next == 'n' || next == 'r' || next == 't' || next == '\\' || next == '"')
                        {
                            // This is already an escape sequence, keep it
                            sb.Append(c);
                        }
                        else
                        {
                            // Lone backslash, escape it
                            sb.Append("\\\\");
                        }
                    }
                    else
                    {
                        // Backslash at end, escape it
                        sb.Append("\\\\");
                    }
                    break;
                    
                case '\n':
                    sb.Append("\\n");
                    break;
                    
                case '\r':
                    sb.Append("\\r");
                    break;
                    
                case '\t':
                    sb.Append("\\t");
                    break;
                    
                default:
                    sb.Append(c);
                    break;
            }
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Delete all backup files for a locale
    /// </summary>
    public Task DeleteBackupsAsync(string localizationDir, string localeCode)
    {
        var filePath = Path.Combine(localizationDir, $"{localeCode}.lua");
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (directory != null && fileSystem.DirectoryExists(directory))
        {
            var backupPattern = $"{fileName}.*{BackupExtension}";
            var backupFiles = fileSystem.GetFiles(directory, backupPattern, SearchOption.TopDirectoryOnly);

            foreach (var backup in backupFiles)
            {
                fileSystem.DeleteFile(backup);
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

        if (directory == null || !fileSystem.DirectoryExists(directory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory}");
        }

        var backupPattern = $"{fileName}.*{BackupExtension}";
        var backupFiles = fileSystem.GetFiles(directory, backupPattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => f)
            .ToArray();

        if (backupFiles.Length == 0)
        {
            throw new FileNotFoundException($"No backup found for {localeCode}.lua");
        }

        var mostRecentBackup = backupFiles[0];
        var content = await fileSystem.ReadAllTextAsync(mostRecentBackup);
        await fileSystem.WriteAllTextAsync(filePath, content);
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
