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

            // FIRST PASS: Find assignment range and identify the LAST occurrence of each key
            var lastOccurrenceIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

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

                    // Extract the key and track its last occurrence
                    var keyMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"L\[""([^""]+)""\]");
                    if (keyMatch.Success)
                    {
                        var key = keyMatch.Groups[1].Value;
                        lastOccurrenceIndex[key] = i; // Overwrites previous, so we get the last one
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[FileWriter] Assignment range: {firstAssignmentIndex} to {lastAssignmentIndex}");

            // Log duplicates found
            var duplicateKeys = lastOccurrenceIndex
                .Where(kvp => CountKeyOccurrences(existingLines, kvp.Key, firstAssignmentIndex, lastAssignmentIndex) > 1)
                .Select(kvp => kvp.Key)
                .ToList();
            if (duplicateKeys.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[FileWriter] Found {duplicateKeys.Count} keys with duplicates: {string.Join(", ", duplicateKeys.Take(10))}{(duplicateKeys.Count > 10 ? "..." : "")}");
            }

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

                // SECOND PASS: Process existing assignments, keeping only the LAST occurrence of each key
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

                            // Extract the original value from the line for logging
                            var originalValue = ExtractValueFromLine(trimmed);

                            // Check if this is NOT the last occurrence of this key - if so, skip it
                            if (lastOccurrenceIndex.TryGetValue(key, out var lastIndex) && i < lastIndex)
                            {
                                skippedDuplicates++;
                                System.Diagnostics.Debug.WriteLine($"[FileWriter] Skipping earlier duplicate at line {i}: {key} (keeping line {lastIndex})");
                                continue;
                            }

                            // This is the last occurrence - process it
                            if (translations.TryGetValue(key, out var newValue))
                            {
                                // Mark as processed
                                processedKeys.Add(key);

                                if (!string.IsNullOrWhiteSpace(newValue))
                                {
                                    // Log if the value is changing (comparing file value to dictionary value)
                                    if (originalValue != null && originalValue != newValue)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[FileWriter] VALUE CHANGED for '{key}':");
                                        System.Diagnostics.Debug.WriteLine($"[FileWriter]   Original: '{originalValue}'");
                                        System.Diagnostics.Debug.WriteLine($"[FileWriter]   New:      '{newValue}'");
                                    }

                                    var lineIndent = GetIndentation(line);
                                    result.Add($"{lineIndent}L[\"{key}\"] = \"{EscapeLuaString(newValue)}\"");
                                    written++;
                                }
                                else
                                {
                                    // Empty value means we want to REMOVE this entry
                                    System.Diagnostics.Debug.WriteLine($"[FileWriter] Removing entry (empty value): {key}");
                                }
                            }
                            else
                            {
                                skippedOrphans++;
                                System.Diagnostics.Debug.WriteLine($"[FileWriter] Skipping orphan: {key}");
                            }
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

        // Has locale block - use similar two-pass approach
        // FIRST PASS: Find the last occurrence of each key within the locale block
        var lastOccurrenceInBlock = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = localeBlockStartIndex + 1; i < localeBlockEndIndex; i++)
        {
            var trimmed = existingLines[i].TrimStart();
            if (trimmed.StartsWith("L[\""))
            {
                var keyMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"L\[""([^""]+)""\]");
                if (keyMatch.Success)
                {
                    var key = keyMatch.Groups[1].Value;
                    lastOccurrenceInBlock[key] = i;
                }
            }
        }

        // Copy everything before the locale block (including the if line)
        for (var i = 0; i <= localeBlockStartIndex; i++)
        {
            result.Add(existingLines[i]);
        }

        // SECOND PASS: Process locale block content, keeping only the LAST occurrence of each key
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

                    // Skip if this is NOT the last occurrence
                    if (lastOccurrenceInBlock.TryGetValue(key, out var lastIndex) && i < lastIndex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileWriter] Skipping earlier duplicate in locale block at line {i}: {key} (keeping line {lastIndex})");
                        continue;
                    }

                    // This is the last occurrence - process it
                    if (translations.TryGetValue(key, out var newValue))
                    {
                        // Mark as processed
                        processedKeys.Add(key);

                        if (!string.IsNullOrWhiteSpace(newValue))
                        {
                            var lineIndent = GetIndentation(line);
                            result.Add($"{lineIndent}L[\"{key}\"] = \"{EscapeLuaString(newValue)}\"");
                        }
                        else
                        {
                            // Empty value means we want to REMOVE this entry
                            System.Diagnostics.Debug.WriteLine($"[FileWriter] Removing entry in locale block (empty value): {key}");
                        }
                    }
                    // If not in translations, it's orphaned - skip it
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
    /// Count how many times a key appears in the assignment range
    /// </summary>
    private static int CountKeyOccurrences(List<string> lines, string key, int startIndex, int endIndex)
    {
        var count = 0;
        for (var i = startIndex; i <= endIndex; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith($"L[\"{key}\"]", StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }
        return count;
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
    /// Extract the string value from a Lua assignment line for logging purposes.
    /// Example: L["key"] = "value" returns "value"
    /// </summary>
    private static string? ExtractValueFromLine(string line)
    {
        var assignmentIndex = line.IndexOf('=');
        if (assignmentIndex < 0 || assignmentIndex >= line.Length - 1)
            return null;

        var rightSide = line[(assignmentIndex + 1)..].Trim();

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

    /// <summary>
    /// Find the closing quote character, handling escape sequences.
    /// </summary>
    private static int FindClosingQuote(string text, char quoteChar, int startIndex)
    {
        for (var i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++; // Skip escaped character
                continue;
            }
            if (text[i] == quoteChar)
            {
                return i;
            }
        }
        return -1;
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

                        // Check for standard escape sequences: \n, \r, \t, \\, \"
                        if (next == 'n' || next == 'r' || next == 't' || next == '\\' || next == '"')
                        {
                            // This is already an escape sequence, keep it
                            sb.Append(c);
                        }
                        // Check for Lua decimal byte escape sequences: \ddd (1-3 digits)
                        else if (char.IsDigit(next))
                        {
                            // This is a Lua numeric escape sequence like \226, keep it
                            sb.Append(c);
                        }
                        // Check for Lua hex escape sequences: \xXX
                        else if (next == 'x' && i + 3 < value.Length && 
                                 IsHexDigit(value[i + 2]) && IsHexDigit(value[i + 3]))
                        {
                            // This is a Lua hex escape sequence like \x1F, keep it
                            sb.Append(c);
                        }
                        // Check for Lua Unicode escape sequences: \u{XXXX}
                        else if (next == 'u' && i + 2 < value.Length && value[i + 2] == '{')
                        {
                            // This is a Lua Unicode escape sequence like \u{1F600}, keep it
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
    /// Check if a character is a hexadecimal digit (0-9, a-f, A-F)
    /// </summary>
    private static bool IsHexDigit(char c)
    {
        return char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
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
