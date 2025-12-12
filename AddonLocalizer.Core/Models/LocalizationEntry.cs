namespace AddonLocalizer.Core.Models;

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

    public static bool IsValidLocale(string localeCode) =>
        SupportedLocales.Any(l => l.Code.Equals(localeCode, StringComparison.OrdinalIgnoreCase));

    public static LocaleInfo? GetLocale(string localeCode) =>
        SupportedLocales.FirstOrDefault(l => l.Code.Equals(localeCode, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Container for all localization data across multiple locales
/// </summary>
public class LocalizationDataSet
{
    // Locale code -> (GlueString -> Translation)
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);

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
    /// Get all translations for a specific locale
    /// </summary>
    public Dictionary<string, string>? GetLocaleData(string localeCode)
    {
        _translations.TryGetValue(localeCode, out var data);
        return data;
    }

    /// <summary>
    /// Get list of all loaded locale codes
    /// </summary>
    public IEnumerable<string> LoadedLocales => _translations.Keys;

    /// <summary>
    /// Get count of translations for a specific locale
    /// </summary>
    public int GetTranslationCount(string localeCode)
    {
        return _translations.TryGetValue(localeCode, out var data) ? data.Count : 0;
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
}