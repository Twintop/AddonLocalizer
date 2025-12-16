using AddonLocalizer.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AddonLocalizer.Models;

public partial class LocalizationEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _glueString;

    [ObservableProperty]
    private int _occurrenceCount;

    [ObservableProperty]
    private bool _hasConcatenation;

    [ObservableProperty]
    private bool _usedInStringFormat;

    [ObservableProperty]
    private int _parameterCount;

    [ObservableProperty]
    private string _fileLocations;

    // Locale translations (in WoW locale order)
    [ObservableProperty]
    private string _enUS = string.Empty;

    [ObservableProperty]
    private string _enGB = string.Empty;

    [ObservableProperty]
    private string _enTW = string.Empty;

    [ObservableProperty]
    private string _enCN = string.Empty;

    [ObservableProperty]
    private string _deDE = string.Empty;

    [ObservableProperty]
    private string _esES = string.Empty;

    [ObservableProperty]
    private string _esMX = string.Empty;

    [ObservableProperty]
    private string _frFR = string.Empty;

    [ObservableProperty]
    private string _itIT = string.Empty;

    [ObservableProperty]
    private string _koKR = string.Empty;

    [ObservableProperty]
    private string _ptBR = string.Empty;

    [ObservableProperty]
    private string _ptPT = string.Empty;

    [ObservableProperty]
    private string _ruRU = string.Empty;

    [ObservableProperty]
    private string _zhCN = string.Empty;

    [ObservableProperty]
    private string _zhTW = string.Empty;

    // GT (Google Translate) status properties - indicates if translation exists in GT file
    [ObservableProperty]
    private bool _hasDeGT;

    [ObservableProperty]
    private bool _hasEsGT;

    [ObservableProperty]
    private bool _hasFrGT;

    [ObservableProperty]
    private bool _hasItGT;

    [ObservableProperty]
    private bool _hasKoGT;

    [ObservableProperty]
    private bool _hasPtGT;

    [ObservableProperty]
    private bool _hasRuGT;

    [ObservableProperty]
    private bool _hasZhCNGT;

    [ObservableProperty]
    private bool _hasZhTWGT;

    // Store reference to original data instead of copying everything
    private readonly GlueStringInfo _sourceInfo;
    
    // Track original translations for change detection
    private readonly Dictionary<string, string> _originalTranslations = new(StringComparer.OrdinalIgnoreCase);

    // Track if enUS was changed - used to prompt for re-translation
    private bool _enUSWasChanged;

    public LocalizationEntryViewModel(string glueString, GlueStringInfo info, LocalizationDataSet? localizationData = null)
    {
        _glueString = glueString;
        _occurrenceCount = info.OccurrenceCount;
        _hasConcatenation = info.HasConcatenation;
        _usedInStringFormat = info.UsedInStringFormat;
        _parameterCount = info.ParameterCount;
        _sourceInfo = info;
        
        // Optimized file location summary generation
        _fileLocations = GenerateFileLocationSummary(info);

        // Load translations if available
        if (localizationData != null)
        {
            LoadTranslations(localizationData);
            LoadGTStatus(localizationData);
        }
    }

    /// <summary>
    /// Load translations from the localization dataset
    /// </summary>
    private void LoadTranslations(LocalizationDataSet dataSet)
    {
        EnUS = GetAndStoreTranslation(dataSet, "enUS");
        EnGB = GetAndStoreTranslation(dataSet, "enGB");
        EnTW = GetAndStoreTranslation(dataSet, "enTW");
        EnCN = GetAndStoreTranslation(dataSet, "enCN");
        DeDE = GetAndStoreTranslation(dataSet, "deDE");
        EsES = GetAndStoreTranslation(dataSet, "esES");
        EsMX = GetAndStoreTranslation(dataSet, "esMX");
        FrFR = GetAndStoreTranslation(dataSet, "frFR");
        ItIT = GetAndStoreTranslation(dataSet, "itIT");
        KoKR = GetAndStoreTranslation(dataSet, "koKR");
        PtBR = GetAndStoreTranslation(dataSet, "ptBR");
        PtPT = GetAndStoreTranslation(dataSet, "ptPT");
        RuRU = GetAndStoreTranslation(dataSet, "ruRU");
        ZhCN = GetAndStoreTranslation(dataSet, "zhCN");
        ZhTW = GetAndStoreTranslation(dataSet, "zhTW");
    }

    /// <summary>
    /// Load GT status from localization dataset
    /// </summary>
    private void LoadGTStatus(LocalizationDataSet dataSet)
    {
        HasDeGT = dataSet.HasGTTranslation(_glueString, "de");
        HasEsGT = dataSet.HasGTTranslation(_glueString, "es");
        HasFrGT = dataSet.HasGTTranslation(_glueString, "fr");
        HasItGT = dataSet.HasGTTranslation(_glueString, "it");
        HasKoGT = dataSet.HasGTTranslation(_glueString, "ko");
        HasPtGT = dataSet.HasGTTranslation(_glueString, "pt");
        HasRuGT = dataSet.HasGTTranslation(_glueString, "ru");
        HasZhCNGT = dataSet.HasGTTranslation(_glueString, "zh");
        HasZhTWGT = dataSet.HasGTTranslation(_glueString, "zh-TW");
    }

    /// <summary>
    /// Get translation and store original value for change tracking
    /// </summary>
    private string GetAndStoreTranslation(LocalizationDataSet dataSet, string localeCode)
    {
        var translation = dataSet.GetTranslation(_glueString, localeCode) ?? string.Empty;
        _originalTranslations[localeCode] = translation;
        return translation;
    }

    /// <summary>
    /// Check if any translations have been modified
    /// </summary>
    public bool HasChanges()
    {
        return EnUS != _originalTranslations.GetValueOrDefault("enUS", string.Empty) ||
               EnGB != _originalTranslations.GetValueOrDefault("enGB", string.Empty) ||
               EnTW != _originalTranslations.GetValueOrDefault("enTW", string.Empty) ||
               EnCN != _originalTranslations.GetValueOrDefault("enCN", string.Empty) ||
               DeDE != _originalTranslations.GetValueOrDefault("deDE", string.Empty) ||
               EsES != _originalTranslations.GetValueOrDefault("esES", string.Empty) ||
               EsMX != _originalTranslations.GetValueOrDefault("esMX", string.Empty) ||
               FrFR != _originalTranslations.GetValueOrDefault("frFR", string.Empty) ||
               ItIT != _originalTranslations.GetValueOrDefault("itIT", string.Empty) ||
               KoKR != _originalTranslations.GetValueOrDefault("koKR", string.Empty) ||
               PtBR != _originalTranslations.GetValueOrDefault("ptBR", string.Empty) ||
               PtPT != _originalTranslations.GetValueOrDefault("ptPT", string.Empty) ||
               RuRU != _originalTranslations.GetValueOrDefault("ruRU", string.Empty) ||
               ZhCN != _originalTranslations.GetValueOrDefault("zhCN", string.Empty) ||
               ZhTW != _originalTranslations.GetValueOrDefault("zhTW", string.Empty);
    }

    /// <summary>
    /// Get dictionary of current translations across all locales
    /// </summary>
    public Dictionary<string, string> GetCurrentTranslations()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["enUS"] = EnUS,
            ["enGB"] = EnGB,
            ["enTW"] = EnTW,
            ["enCN"] = EnCN,
            ["deDE"] = DeDE,
            ["esES"] = EsES,
            ["esMX"] = EsMX,
            ["frFR"] = FrFR,
            ["itIT"] = ItIT,
            ["koKR"] = KoKR,
            ["ptBR"] = PtBR,
            ["ptPT"] = PtPT,
            ["ruRU"] = RuRU,
            ["zhCN"] = ZhCN,
            ["zhTW"] = ZhTW
        };
    }

    /// <summary>
    /// Get dictionary of only changed translations
    /// </summary>
    public Dictionary<string, string> GetChangedTranslations()
    {
        var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        CheckAndAddChange(changes, "enUS", EnUS);
        CheckAndAddChange(changes, "enGB", EnGB);
        CheckAndAddChange(changes, "enTW", EnTW);
        CheckAndAddChange(changes, "enCN", EnCN);
        CheckAndAddChange(changes, "deDE", DeDE);
        CheckAndAddChange(changes, "esES", EsES);
        CheckAndAddChange(changes, "esMX", EsMX);
        CheckAndAddChange(changes, "frFR", FrFR);
        CheckAndAddChange(changes, "itIT", ItIT);
        CheckAndAddChange(changes, "koKR", KoKR);
        CheckAndAddChange(changes, "ptBR", PtBR);
        CheckAndAddChange(changes, "ptPT", PtPT);
        CheckAndAddChange(changes, "ruRU", RuRU);
        CheckAndAddChange(changes, "zhCN", ZhCN);
        CheckAndAddChange(changes, "zhTW", ZhTW);

        return changes;
    }

    private void CheckAndAddChange(Dictionary<string, string> changes, string localeCode, string currentValue)
    {
        var originalValue = _originalTranslations.GetValueOrDefault(localeCode, string.Empty);
        if (currentValue != originalValue)
        {
            changes[localeCode] = currentValue;
        }
    }

    /// <summary>
    /// Reset translations to their original values
    /// </summary>
    public void ResetChanges()
    {
        EnUS = _originalTranslations.GetValueOrDefault("enUS", string.Empty);
        EnGB = _originalTranslations.GetValueOrDefault("enGB", string.Empty);
        EnTW = _originalTranslations.GetValueOrDefault("enTW", string.Empty);
        EnCN = _originalTranslations.GetValueOrDefault("enCN", string.Empty);
        DeDE = _originalTranslations.GetValueOrDefault("deDE", string.Empty);
        EsES = _originalTranslations.GetValueOrDefault("esES", string.Empty);
        EsMX = _originalTranslations.GetValueOrDefault("esMX", string.Empty);
        FrFR = _originalTranslations.GetValueOrDefault("frFR", string.Empty);
        ItIT = _originalTranslations.GetValueOrDefault("itIT", string.Empty);
        KoKR = _originalTranslations.GetValueOrDefault("koKR", string.Empty);
        PtBR = _originalTranslations.GetValueOrDefault("ptBR", string.Empty);
        PtPT = _originalTranslations.GetValueOrDefault("ptPT", string.Empty);
        RuRU = _originalTranslations.GetValueOrDefault("ruRU", string.Empty);
        ZhCN = _originalTranslations.GetValueOrDefault("zhCN", string.Empty);
        ZhTW = _originalTranslations.GetValueOrDefault("zhTW", string.Empty);
    }

    /// <summary>
    /// Commit current values as the new baseline (after successful save)
    /// </summary>
    public void CommitChanges()
    {
        _originalTranslations["enUS"] = EnUS;
        _originalTranslations["enGB"] = EnGB;
        _originalTranslations["enTW"] = EnTW;
        _originalTranslations["enCN"] = EnCN;
        _originalTranslations["deDE"] = DeDE;
        _originalTranslations["esES"] = EsES;
        _originalTranslations["esMX"] = EsMX;
        _originalTranslations["frFR"] = FrFR;
        _originalTranslations["itIT"] = ItIT;
        _originalTranslations["koKR"] = KoKR;
        _originalTranslations["ptBR"] = PtBR;
        _originalTranslations["ptPT"] = PtPT;
        _originalTranslations["ruRU"] = RuRU;
        _originalTranslations["zhCN"] = ZhCN;
        _originalTranslations["zhTW"] = ZhTW;
    }

    /// <summary>
    /// Check if the enUS translation has been modified from its original value
    /// </summary>
    public bool HasEnUSChanged()
    {
        return EnUS != _originalTranslations.GetValueOrDefault("enUS", string.Empty);
    }

    /// <summary>
    /// Get the original enUS value before any changes
    /// </summary>
    public string GetOriginalEnUS()
    {
        return _originalTranslations.GetValueOrDefault("enUS", string.Empty);
    }

    /// <summary>
    /// Check if there are any non-enUS manual translations that could be affected by enUS change
    /// This includes other English variants (enGB, enTW, enCN) as well as all other languages
    /// </summary>
    public bool HasNonEnUSManualTranslations()
    {
        return !string.IsNullOrWhiteSpace(EnGB) ||
               !string.IsNullOrWhiteSpace(EnTW) ||
               !string.IsNullOrWhiteSpace(EnCN) ||
               !string.IsNullOrWhiteSpace(DeDE) ||
               !string.IsNullOrWhiteSpace(EsES) ||
               !string.IsNullOrWhiteSpace(EsMX) ||
               !string.IsNullOrWhiteSpace(FrFR) ||
               !string.IsNullOrWhiteSpace(ItIT) ||
               !string.IsNullOrWhiteSpace(KoKR) ||
               !string.IsNullOrWhiteSpace(PtBR) ||
               !string.IsNullOrWhiteSpace(PtPT) ||
               !string.IsNullOrWhiteSpace(RuRU) ||
               !string.IsNullOrWhiteSpace(ZhCN) ||
               !string.IsNullOrWhiteSpace(ZhTW);
    }

    /// <summary>
    /// Clear all non-enUS manual translations (locale files, not GT)
    /// This includes other English variants (enGB, enTW, enCN) as well as all other languages
    /// </summary>
    public void ClearNonEnUSTranslations()
    {
        EnGB = string.Empty;
        EnTW = string.Empty;
        EnCN = string.Empty;
        DeDE = string.Empty;
        EsES = string.Empty;
        EsMX = string.Empty;
        FrFR = string.Empty;
        ItIT = string.Empty;
        KoKR = string.Empty;
        PtBR = string.Empty;
        PtPT = string.Empty;
        RuRU = string.Empty;
        ZhCN = string.Empty;
        ZhTW = string.Empty;
    }

    /// <summary>
    /// Get list of non-enUS locales that have manual translations
    /// This includes other English variants (enGB, enTW, enCN) as well as all other languages
    /// </summary>
    public List<string> GetNonEnUSLocalesWithTranslations()
    {
        var locales = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(EnGB)) locales.Add("enGB");
        if (!string.IsNullOrWhiteSpace(EnTW)) locales.Add("enTW");
        if (!string.IsNullOrWhiteSpace(EnCN)) locales.Add("enCN");
        if (!string.IsNullOrWhiteSpace(DeDE)) locales.Add("deDE");
        if (!string.IsNullOrWhiteSpace(EsES)) locales.Add("esES");
        if (!string.IsNullOrWhiteSpace(EsMX)) locales.Add("esMX");
        if (!string.IsNullOrWhiteSpace(FrFR)) locales.Add("frFR");
        if (!string.IsNullOrWhiteSpace(ItIT)) locales.Add("itIT");
        if (!string.IsNullOrWhiteSpace(KoKR)) locales.Add("koKR");
        if (!string.IsNullOrWhiteSpace(PtBR)) locales.Add("ptBR");
        if (!string.IsNullOrWhiteSpace(PtPT)) locales.Add("ptPT");
        if (!string.IsNullOrWhiteSpace(RuRU)) locales.Add("ruRU");
        if (!string.IsNullOrWhiteSpace(ZhCN)) locales.Add("zhCN");
        if (!string.IsNullOrWhiteSpace(ZhTW)) locales.Add("zhTW");
        
        return locales;
    }

    /// <summary>
    /// Mark that the enUS value was changed (for tracking re-translation needs)
    /// </summary>
    public void MarkEnUSChanged()
    {
        _enUSWasChanged = true;
    }

    /// <summary>
    /// Check if enUS was marked as changed (for re-translation tracking)
    /// </summary>
    public bool WasEnUSChanged => _enUSWasChanged;

    /// <summary>
    /// Clear the enUS changed flag (after re-translation is complete)
    /// </summary>
    public void ClearEnUSChangedFlag()
    {
        _enUSWasChanged = false;
    }

    /// <summary>
    /// Calculate translation coverage percentage
    /// </summary>
    public double TranslationCoverage
    {
        get
        {
            var translations = GetCurrentTranslations();
            var totalLocales = translations.Count;
            var translatedCount = translations.Values.Count(v => !string.IsNullOrWhiteSpace(v));
            return totalLocales > 0 ? (translatedCount / (double)totalLocales) * 100 : 0;
        }
    }

    /// <summary>
    /// Check if this entry is missing any translations
    /// </summary>
    public bool IsMissingTranslations => TranslationCoverage < 100;

    /// <summary>
    /// Get count of locales with translations
    /// </summary>
    public int TranslatedLocaleCount
    {
        get
        {
            var translations = GetCurrentTranslations();
            return translations.Values.Count(v => !string.IsNullOrWhiteSpace(v));
        }
    }

    private static string GenerateFileLocationSummary(GlueStringInfo info)
    {
        if (info.Locations.Count == 0)
        {
            return "N/A";
        }

        // Use HashSet for efficient distinct file tracking
        var distinctFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var displayFiles = new List<string>(3);
        
        // Single pass through locations
        foreach (var location in info.Locations)
        {
            var fileName = Path.GetFileName(location.FilePath);
            if (distinctFiles.Add(fileName))
            {
                displayFiles.Add(fileName);
            }
        }

        var summary = string.Join(", ", displayFiles);

        return summary;
    }

    // Lazy-load these only when detail view is accessed
    public List<GlueStringLocation> GetLocations() => _sourceInfo.Locations;
    public List<FormatParameter> GetFormatParameters() => _sourceInfo.FormatParameters;

    public string ConcatenationIcon => HasConcatenation ? "X" : "";
    public string StringFormatIcon
    {
        get
        {
            if (!UsedInStringFormat) return "";
            
            var formatParams = _sourceInfo.FormatParameters;
            if (formatParams == null || formatParams.Count == 0)
            {
                return "X"; // Has string.format but no parameter info
            }
            
            // Filter out percent signs (they're escape sequences, not parameters)
            var actualParams = formatParams
                .Where(p => p.Type != FormatParameterType.Percent)
                .ToList();
            
            if (actualParams.Count == 0)
            {
                return "X";
            }
            
            // List each parameter's type in order (don't group/deduplicate)
            var paramTypes = actualParams.Select(p => p.Type.ToString()).ToList();
            
            return $"{actualParams.Count} - {string.Join(", ", paramTypes)}";
        }
    }

    // GT status icon properties - show green checkmark or red X
    // Using Unicode escape sequences for reliability across fonts
    public string DeGTIcon => HasDeGT ? "\u2714" : "\u2718";
    public string EsGTIcon => HasEsGT ? "\u2714" : "\u2718";
    public string FrGTIcon => HasFrGT ? "\u2714" : "\u2718";
    public string ItGTIcon => HasItGT ? "\u2714" : "\u2718";
    public string KoGTIcon => HasKoGT ? "\u2714" : "\u2718";
    public string PtGTIcon => HasPtGT ? "\u2714" : "\u2718";
    public string RuGTIcon => HasRuGT ? "\u2714" : "\u2718";
    public string ZhCNGTIcon => HasZhCNGT ? "\u2714" : "\u2718";
    public string ZhTWGTIcon => HasZhTWGT ? "\u2714" : "\u2718";
}
