namespace AddonLocalizer.Core.Models;

/// <summary>
/// Represents a duplicate localization entry found in a locale file
/// </summary>
public class DuplicateEntry
{
    /// <summary>
    /// The localization key that has duplicates
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// All values found for this key, in order of appearance
    /// </summary>
    public List<string> Values { get; set; } = [];
    
    /// <summary>
    /// The value that will be used (the last one)
    /// </summary>
    public string FinalValue => Values.Count > 0 ? Values[^1] : string.Empty;
    
    /// <summary>
    /// Number of times this key appears
    /// </summary>
    public int OccurrenceCount => Values.Count;
}

public class GlueStringLocation
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}

public enum FormatParameterType
{
    String,      // %s
    Integer,     // %d, %i
    Float,       // %f
    Character,   // %c
    Unsigned,    // %u
    Hexadecimal, // %x, %X
    Octal,       // %o
    Exponential, // %e, %E
    General,     // %g, %G
    Percent      // %%
}

public class FormatParameter
{
    public int Position { get; set; }  // Position in format string (1-based for positional, 0 for sequential)
    public FormatParameterType Type { get; set; }
    public string RawSpecifier { get; set; } = string.Empty;  // e.g., "%s", "%1$s", "%.2f"
    public int? Width { get; set; }
    public int? Precision { get; set; }
    public bool HasPositionalIndex { get; set; }
}

public class GlueStringInfo
{
    public string GlueString { get; set; } = string.Empty;
    public bool HasConcatenation { get; set; }
    public bool UsedInStringFormat { get; set; }
    public int OccurrenceCount { get; set; }
    public List<GlueStringLocation> Locations { get; set; } = [];
    public List<GlueStringLocation> StringFormatLocations { get; set; } = [];
    public List<FormatParameter> FormatParameters { get; set; } = [];
    
    public int ParameterCount => FormatParameters.Count(p => p.Type != FormatParameterType.Percent);
}

public class ParseResult
{
    public Dictionary<string, GlueStringInfo> GlueStrings { get; set; } = new();
        
    public IEnumerable<GlueStringInfo> NonConcatenated => 
        GlueStrings.Values.Where(g => !g.HasConcatenation);
        
    public IEnumerable<GlueStringInfo> Concatenated => 
        GlueStrings.Values.Where(g => g.HasConcatenation);
    
    public IEnumerable<GlueStringInfo> WithStringFormat =>
        GlueStrings.Values.Where(g => g.UsedInStringFormat);
}

// Multi-locale support models

/// <summary>
/// Defines a supported WoW locale
/// </summary>
public record LocaleInfo(string Code, string DisplayName, int SortOrder);

/// <summary>
/// Centralized locale definitions for WoW
/// </summary>
public static class LocaleDefinitions
{
    public static readonly List<LocaleInfo> SupportedLocales =
    [
        new("enUS", "English (US)", 0),
        new("enGB", "English (GB)", 1),
        new("enTW", "English (TW)", 2),
        new("enCN", "English (CN)", 3),
        new("deDE", "German", 4),
        new("esES", "Spanish (ES)", 5),
        new("esMX", "Spanish (MX)", 6),
        new("frFR", "French", 7),
        new("itIT", "Italian", 8),
        new("koKR", "Korean", 9),
        new("ptBR", "Portuguese (BR)", 10),
        new("ptPT", "Portuguese (PT)", 11),
        new("ruRU", "Russian", 12),
        new("zhCN", "Chinese (Simplified)", 13),
        new("zhTW", "Chinese (Traditional)", 14)
    ];

    /// <summary>
    /// Maps WoW locales to their base language code and Google Translate language code
    /// </summary>
    public static readonly Dictionary<string, (string BaseLocale, string GoogleLanguageCode)> LocaleToBaseMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // English locales - excluded from GT
        { "enUS", ("en", "en") },
        { "enGB", ("en", "en") },
        { "enTW", ("en", "en") },
        { "enCN", ("en", "en") },
        
        // German
        { "deDE", ("de", "de") },
        
        // Spanish
        { "esES", ("es", "es") },
        { "esMX", ("es", "es") },
        
        // French
        { "frFR", ("fr", "fr") },
        
        // Italian
        { "itIT", ("it", "it") },
        
        // Korean
        { "koKR", ("ko", "ko") },
        
        // Portuguese
        { "ptBR", ("pt", "pt") },
        { "ptPT", ("pt", "pt") },
        
        // Russian
        { "ruRU", ("ru", "ru") },
        
        // Chinese
        { "zhCN", ("zh", "zh-CN") },
        { "zhTW", ("zh-TW", "zh-TW") }
    };

    public static bool IsValidLocale(string localeCode) =>
        SupportedLocales.Any(l => l.Code.Equals(localeCode, StringComparison.OrdinalIgnoreCase));

    public static LocaleInfo? GetLocale(string localeCode) =>
        SupportedLocales.FirstOrDefault(l => l.Code.Equals(localeCode, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the GT file suffix for a locale (e.g., "de-GT" for deDE)
    /// </summary>
    public static string GetGTFileSuffix(string localeCode)
    {
        if (LocaleToBaseMapping.TryGetValue(localeCode, out var mapping))
        {
            return $"{mapping.BaseLocale}-GT";
        }
        return string.Empty;
    }

    /// <summary>
    /// Gets all unique base locales that support GT (excludes English)
    /// </summary>
    public static IEnumerable<string> GetGTBaseLocales() =>
        LocaleToBaseMapping
            .Where(kvp => !kvp.Key.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Value.BaseLocale)
            .Distinct();

    /// <summary>
    /// Gets the Google Translate language code for a base locale
    /// </summary>
    public static string? GetGoogleLanguageCode(string baseLocale)
    {
        var entry = LocaleToBaseMapping.Values.FirstOrDefault(v => v.BaseLocale == baseLocale);
        return entry.GoogleLanguageCode;
    }

    /// <summary>
    /// Gets all WoW locales that share a base locale
    /// </summary>
    public static IEnumerable<string> GetLocalesForBase(string baseLocale) =>
        LocaleToBaseMapping
            .Where(kvp => kvp.Value.BaseLocale == baseLocale)
            .Select(kvp => kvp.Key);

    /// <summary>
    /// Checks if a locale is an English locale (excluded from GT)
    /// </summary>
    public static bool IsEnglishLocale(string localeCode) =>
        localeCode.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the GT filename for a base locale.
    /// Chinese locales use WoW locale codes (zhCN-GT.lua, zhTW-GT.lua).
    /// Other locales use the base locale (es-GT.lua, pt-GT.lua, etc.).
    /// </summary>
    public static string GetGTFileName(string baseLocale) =>
        baseLocale switch
        {
            "zh" => "zhCN-GT.lua",
            "zh-TW" => "zhTW-GT.lua",
            _ => $"{baseLocale}-GT.lua"
        };
}

/// <summary>
/// Container for all localization data across multiple locales
/// </summary>
public class LocalizationDataSet
{
    // Locale code -> (GlueString -> Translation)
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);

    // GT translations: BaseLocale (e.g., "de") -> (GlueString -> Translation)
    private readonly Dictionary<string, Dictionary<string, string>> _gtTranslations = new(StringComparer.OrdinalIgnoreCase);

    // Duplicate entries per locale: Locale code -> List of duplicate entries
    private readonly Dictionary<string, List<DuplicateEntry>> _duplicates = new(StringComparer.OrdinalIgnoreCase);

    // All unique glue strings across all locales
    public HashSet<string> AllGlueStrings { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Add translations for a specific locale
    /// </summary>
    public void AddLocale(string localeCode, Dictionary<string, string> translations)
    {
        _translations[localeCode] = translations;
        
        foreach (var key in translations.Keys)
        {
            AllGlueStrings.Add(key);
        }
    }

    /// <summary>
    /// Add translations for a specific locale, along with any detected duplicates
    /// </summary>
    public void AddLocale(string localeCode, Dictionary<string, string> translations, List<DuplicateEntry>? duplicates)
    {
        AddLocale(localeCode, translations);
        
        if (duplicates != null && duplicates.Count > 0)
        {
            _duplicates[localeCode] = duplicates;
        }
    }

    /// <summary>
    /// Add GT translations for a base locale
    /// </summary>
    public void AddGTLocale(string baseLocale, Dictionary<string, string> translations)
    {
        _gtTranslations[baseLocale] = translations;
    }

    /// <summary>
    /// Get translation for a specific glue string and locale
    /// </summary>
    public string? GetTranslation(string glueString, string localeCode)
    {
        if (_translations.TryGetValue(localeCode, out var localeData))
        {
            localeData.TryGetValue(glueString, out var translation);
            return translation;
        }
        return null;
    }

    /// <summary>
    /// Get GT translation for a glue string in a base locale
    /// </summary>
    public string? GetGTTranslation(string glueString, string baseLocale)
    {
        if (_gtTranslations.TryGetValue(baseLocale, out var localeData))
        {
            localeData.TryGetValue(glueString, out var translation);
            return translation;
        }
        return null;
    }

    /// <summary>
    /// Check if GT translation exists for a glue string in a base locale
    /// </summary>
    public bool HasGTTranslation(string glueString, string baseLocale)
    {
        return _gtTranslations.TryGetValue(baseLocale, out var localeData) 
            && localeData.ContainsKey(glueString);
    }

    /// <summary>
    /// Get all translations for a specific locale
    /// </summary>
    public Dictionary<string, string>? GetLocaleData(string localeCode)
    {
        _translations.TryGetValue(localeCode, out var data);
        return data;
    }

    /// <summary>
    /// Get all GT translations for a base locale
    /// </summary>
    public Dictionary<string, string>? GetGTLocaleData(string baseLocale)
    {
        _gtTranslations.TryGetValue(baseLocale, out var data);
        return data;
    }

    /// <summary>
    /// Get list of all loaded locale codes
    /// </summary>
    public IEnumerable<string> LoadedLocales => _translations.Keys;

    /// <summary>
    /// Get list of loaded GT base locales
    /// </summary>
    public IEnumerable<string> LoadedGTLocales => _gtTranslations.Keys;

    /// <summary>
    /// Get all locales that have duplicate entries
    /// </summary>
    public IEnumerable<string> LocalesWithDuplicates => _duplicates.Keys;

    /// <summary>
    /// Check if any locale has duplicates
    /// </summary>
    public bool HasDuplicates => _duplicates.Count > 0;

    /// <summary>
    /// Get total count of duplicate entries across all locales
    /// </summary>
    public int TotalDuplicateCount => _duplicates.Values.Sum(d => d.Count);

    /// <summary>
    /// Get duplicates for a specific locale
    /// </summary>
    public List<DuplicateEntry> GetDuplicates(string localeCode)
    {
        return _duplicates.TryGetValue(localeCode, out var duplicates) ? duplicates : [];
    }

    /// <summary>
    /// Get all duplicates across all locales, grouped by locale
    /// </summary>
    public Dictionary<string, List<DuplicateEntry>> GetAllDuplicates() => 
        new(_duplicates, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Get count of translations for a specific locale
    /// </summary>
    public int GetTranslationCount(string localeCode)
    {
        return _translations.TryGetValue(localeCode, out var data) ? data.Count : 0;
    }

    /// <summary>
    /// Get count of GT translations for a base locale
    /// </summary>
    public int GetGTTranslationCount(string baseLocale)
    {
        return _gtTranslations.TryGetValue(baseLocale, out var data) ? data.Count : 0;
    }

    /// <summary>
    /// Calculate translation coverage percentage for a locale
    /// </summary>
    public double GetCoveragePercentage(string localeCode)
    {
        if (AllGlueStrings.Count == 0) return 0;
        return (GetTranslationCount(localeCode) / (double)AllGlueStrings.Count) * 100;
    }

    /// <summary>
    /// Check if a locale has been loaded
    /// </summary>
    public bool HasLocale(string localeCode) => _translations.ContainsKey(localeCode);

    /// <summary>
    /// Check if a GT locale has been loaded
    /// </summary>
    public bool HasGTLocale(string baseLocale) => _gtTranslations.ContainsKey(baseLocale);

    /// <summary>
    /// Get orphaned glue strings for a specific locale (entries in locale file but not in valid keys)
    /// </summary>
    public List<string> GetOrphanedKeysForLocale(string localeCode, HashSet<string> validKeys)
    {
        if (!_translations.TryGetValue(localeCode, out var localeData))
            return [];

        return localeData.Keys
            .Where(k => !validKeys.Contains(k))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Get orphaned glue strings for a specific GT locale (entries in GT file but not in valid keys)
    /// </summary>
    public List<string> GetOrphanedKeysForGTLocale(string baseLocale, HashSet<string> validKeys)
    {
        if (!_gtTranslations.TryGetValue(baseLocale, out var localeData))
            return [];

        return localeData.Keys
            .Where(k => !validKeys.Contains(k))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Get a summary of all files containing orphaned entries
    /// </summary>
    public Dictionary<string, List<string>> GetOrphanedEntriesByFile(HashSet<string> validKeys)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Check regular locale files
        foreach (var localeCode in _translations.Keys)
        {
            var orphaned = GetOrphanedKeysForLocale(localeCode, validKeys);
            if (orphaned.Count > 0)
            {
                var fileName = $"{localeCode}.lua";
                result[fileName] = orphaned;
            }
        }

        // Check GT locale files
        foreach (var baseLocale in _gtTranslations.Keys)
        {
            var orphaned = GetOrphanedKeysForGTLocale(baseLocale, validKeys);
            if (orphaned.Count > 0)
            {
                var fileName = LocaleDefinitions.GetGTFileName(baseLocale);
                result[fileName] = orphaned;
            }
        }

        return result;
    }

    /// <summary>
    /// Remove orphaned keys from a locale's translations
    /// </summary>
    public int RemoveOrphanedKeysFromLocale(string localeCode, HashSet<string> validKeys)
    {
        if (!_translations.TryGetValue(localeCode, out var localeData))
            return 0;

        var orphanedKeys = localeData.Keys
            .Where(k => !validKeys.Contains(k))
            .ToList();

        foreach (var key in orphanedKeys)
        {
            localeData.Remove(key);
        }

        return orphanedKeys.Count;
    }

    /// <summary>
    /// Remove orphaned keys from a GT locale's translations
    /// </summary>
    public int RemoveOrphanedKeysFromGTLocale(string baseLocale, HashSet<string> validKeys)
    {
        if (!_gtTranslations.TryGetValue(baseLocale, out var localeData))
            return 0;

        var orphanedKeys = localeData.Keys
            .Where(k => !validKeys.Contains(k))
            .ToList();

        foreach (var key in orphanedKeys)
        {
            localeData.Remove(key);
        }

        return orphanedKeys.Count;
    }
}