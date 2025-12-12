using AddonLocalizer.Core.Services;

namespace AddonLocalizer.Core.Interfaces;

/// <summary>
/// Service for writing localization data to Lua files
/// </summary>
public interface ILocalizationFileWriterService
{
    /// <summary>
    /// Save translations for a specific locale to its file
    /// </summary>
    Task SaveLocaleFileAsync(
        string localizationDir, 
        string localeCode, 
        Dictionary<string, string> translations,
        bool createBackup = true);

    /// <summary>
    /// Save multiple locale files at once with progress reporting
    /// </summary>
    Task SaveMultipleLocaleFilesAsync(
        string localizationDir,
        Dictionary<string, Dictionary<string, string>> localeTranslations,
        bool createBackup = true,
        IProgress<SaveProgress>? progress = null);

    /// <summary>
    /// Delete all backup files for a locale
    /// </summary>
    Task DeleteBackupsAsync(string localizationDir, string localeCode);

    /// <summary>
    /// Restore from the most recent backup
    /// </summary>
    Task RestoreFromBackupAsync(string localizationDir, string localeCode);
}
