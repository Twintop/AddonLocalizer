using System.Collections.ObjectModel;
using AddonLocalizer.Core.Interfaces;
using AddonLocalizer.Core.Models;
using AddonLocalizer.Core.Services;
using AddonLocalizer.Models;
using AddonLocalizer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace AddonLocalizer.PageModels;

public partial class LocalizationGridPageModel : ObservableObject, IQueryAttributable
{
    private readonly ILocalizationFileWriterService _fileWriter;
    private readonly IDialogService _dialogService;
    private readonly IGoogleTranslateService _translateService;
    private readonly ILuaLocalizationParserService _parserService;

    [ObservableProperty]
    private ObservableCollection<LocalizationEntryViewModel> _entries = [];

    [ObservableProperty]
    private ObservableCollection<LocalizationEntryViewModel> _filteredEntries = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showOnlyConcatenated;

    [ObservableProperty]
    private bool _showOnlyStringFormat;

    [ObservableProperty]
    private bool _showOnlyWithParameters;

    [ObservableProperty]
    private string? _missingTranslationLocale;

    [ObservableProperty]
    private ObservableCollection<string> _availableLocales = new()
    {
        "None",
        "Any",
        "enUS",
        "enGB",
        "enTW",
        "enCN",
        "deDE",
        "esES",
        "esMX",
        "frFR",
        "itIT",
        "koKR",
        "ptBR",
        "ptPT",
        "ruRU",
        "zhCN",
        "zhTW"
    };

    [ObservableProperty]
    private LocalizationEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _filteredCount;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private bool _hasData = false;

    [ObservableProperty]
    private bool _isSaving = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private int _modifiedEntryCount;

    [ObservableProperty]
    private double _saveProgress;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private double _translationProgress;

    [ObservableProperty]
    private string _translationStatus = string.Empty;

    private ParseResult? _parseResult;
    private LocalizationDataSet? _localizationData;
    private string? _localizationDirectory;

    public LocalizationGridPageModel(
        ILocalizationFileWriterService fileWriter, 
        IDialogService dialogService,
        IGoogleTranslateService translateService,
        ILuaLocalizationParserService parserService)
    {
        _fileWriter = fileWriter;
        _dialogService = dialogService;
        _translateService = translateService;
        _parserService = parserService;
        Debug.WriteLine("[GridPage] Constructor called");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            if (query.TryGetValue("ParseResult", out var result) && result is ParseResult parseResult)
            {
                _parseResult = parseResult;
                Debug.WriteLine($"[GridPage] Received ParseResult with {parseResult.GlueStrings.Count} glue strings");
                
                // Check for localization data
                if (query.TryGetValue("LocalizationData", out var locData) && locData is LocalizationDataSet localizationDataSet)
                {
                    _localizationData = localizationDataSet;
                    Debug.WriteLine($"[GridPage] Received LocalizationData with {localizationDataSet.LoadedLocales.Count()} locales");
                }
                else
                {
                    Debug.WriteLine($"[GridPage] WARNING: No LocalizationData received!");
                }

                // Check for localization directory path
                if (query.TryGetValue("LocalizationDirectory", out var locDir) && locDir is string localizationDir)
                {
                    _localizationDirectory = localizationDir;
                    Debug.WriteLine($"[GridPage] Received LocalizationDirectory: {localizationDir}");
                }
                else
                {
                    Debug.WriteLine($"[GridPage] WARNING: No LocalizationDirectory received!");
                }
                
                // Defer loading to avoid blocking navigation
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(100); // Small delay to ensure page is fully loaded
                    await LoadDataAsync();
                });
            }
            else
            {
                Debug.WriteLine("[GridPage] No valid ParseResult in query attributes");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GridPage] Error in ApplyQueryAttributes: {ex.Message}");
            Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
            StatusMessage = $"Error applying query: {ex.Message}";
        }
    }

    private async Task LoadDataAsync()
    {
        if (_parseResult == null) return;

        try
        {
            IsLoading = true;
            HasData = false;
            StatusMessage = "Loading entries...";
            Debug.WriteLine("[GridPage] Starting data load...");

            // Reload localization data from disk to get latest GT status
            if (!string.IsNullOrEmpty(_localizationDirectory) && Directory.Exists(_localizationDirectory))
            {
                StatusMessage = "Reloading localization data...";
                Debug.WriteLine($"[GridPage] Reloading localization data from: {_localizationDirectory}");
                
                try
                {
                    // Re-parse the localization directory to get fresh data including GT files
                    _localizationData = await _parserService.ParseLocalizationDirectoryAsync(_localizationDirectory);
                    
                    // Load GT files into the dataset
                    await _parserService.LoadGTFilesAsync(_localizationDirectory, _localizationData);
                    
                    Debug.WriteLine($"[GridPage] Reloaded localization data with {_localizationData.LoadedLocales.Count()} locales");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GridPage] Error reloading localization data: {ex.Message}");
                    // Continue with existing data if reload fails
                }
            }

            // Create view models on background thread
            var entries = await Task.Run(() =>
            {
                try
                {
                    var result = _parseResult.GlueStrings
                        .Select(kvp => 
                        {
                            try
                            {
                                // Pass localization data to ViewModel
                                return new LocalizationEntryViewModel(kvp.Key, kvp.Value, _localizationData);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[GridPage] Error creating ViewModel for '{kvp.Key}': {ex.Message}");
                                Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
                                throw;
                            }
                        })
                        .OrderBy(e => e.GlueString)
                        .ToList();
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GridPage] Error in Task.Run: {ex.Message}");
                    Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
                    throw;
                }
            });

            Debug.WriteLine($"[GridPage] Created {entries.Count} view models");

            // Update UI on main thread - use assignment instead of Clear/Add
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    StatusMessage = $"Loaded {entries.Count} entries";
                    
                    // Create new collection instead of modifying existing
                    Entries = new ObservableCollection<LocalizationEntryViewModel>(entries);
                    TotalCount = entries.Count;
                    
                    ApplyFilters();
                    
                    HasData = FilteredEntries.Count > 0;
                    
                    Debug.WriteLine($"[GridPage] HasData set to: {HasData}, FilteredCount: {FilteredCount}");
                    
                    var localeInfo = _localizationData != null 
                        ? $" with {_localizationData.LoadedLocales.Count()} locales" 
                        : "";
                    StatusMessage = HasData 
                        ? $"Showing {FilteredCount} of {TotalCount} entries{localeInfo}" 
                        : "No entries to display";

                    // Setup property change monitoring for entries
                    foreach (var entry in Entries)
                    {
                        entry.PropertyChanged += Entry_PropertyChanged;
                    }

                    UpdateChangeTracking();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GridPage] Error updating UI: {ex.Message}");
                    Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GridPage] Error in LoadDataAsync: {ex.Message}");
            Debug.WriteLine($"[GridPage] Inner exception: {ex.InnerException?.Message}");
            Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusMessage = $"Error loading data: {ex.Message}";
                HasData = false;
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsLoading = false;
            });
            Debug.WriteLine($"[GridPage] Loading complete. HasData={HasData}, IsLoading={IsLoading}");
        }
    }

    private void Entry_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Check if a locale property changed
        if (e.PropertyName?.StartsWith("En") == true || 
            e.PropertyName?.StartsWith("De") == true ||
            e.PropertyName?.StartsWith("Es") == true ||
            e.PropertyName?.StartsWith("Fr") == true ||
            e.PropertyName?.StartsWith("It") == true ||
            e.PropertyName?.StartsWith("Ko") == true ||
            e.PropertyName?.StartsWith("Pt") == true ||
            e.PropertyName?.StartsWith("Ru") == true ||
            e.PropertyName?.StartsWith("Zh") == true)
        {
            UpdateChangeTracking();
        }
    }

    private void UpdateChangeTracking()
    {
        var modifiedEntries = GetModifiedEntries().ToList();
        ModifiedEntryCount = modifiedEntries.Count;
        HasUnsavedChanges = ModifiedEntryCount > 0;
        
        // Notify command state changes
        SaveChangesCommand.NotifyCanExecuteChanged();
        DiscardChangesCommand.NotifyCanExecuteChanged();
        
        Debug.WriteLine($"[GridPage] Change tracking updated: {ModifiedEntryCount} modified entries");
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnShowOnlyConcatenatedChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnShowOnlyStringFormatChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnShowOnlyWithParametersChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnMissingTranslationLocaleChanged(string? value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (Entries.Count == 0)
        {
            FilteredEntries.Clear();
            FilteredCount = 0;
            HasData = false;
            Debug.WriteLine("[GridPage] ApplyFilters: No entries, HasData=false");
            return;
        }

        var filtered = Entries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(e => 
                e.GlueString.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (ShowOnlyConcatenated)
        {
            filtered = filtered.Where(e => e.HasConcatenation);
        }

        if (ShowOnlyStringFormat)
        {
            filtered = filtered.Where(e => e.UsedInStringFormat);
        }

        if (ShowOnlyWithParameters)
        {
            filtered = filtered.Where(e => e.ParameterCount > 0);
        }

        if (!string.IsNullOrWhiteSpace(MissingTranslationLocale) && MissingTranslationLocale != "None")
        {
            if (MissingTranslationLocale == "Any")
            {
                filtered = filtered.Where(e => e.IsMissingTranslations);
            }
            else
            {
                filtered = filtered.Where(e => IsMissingTranslationForLocale(e, MissingTranslationLocale));
            }
        }

        FilteredEntries = new ObservableCollection<LocalizationEntryViewModel>(filtered);
        FilteredCount = FilteredEntries.Count;
        HasData = FilteredCount > 0;
        
        // Update status message based on filter state
        var hasActiveFilters = !string.IsNullOrWhiteSpace(SearchText) ||
                               ShowOnlyConcatenated ||
                               ShowOnlyStringFormat ||
                               ShowOnlyWithParameters ||
                               (!string.IsNullOrWhiteSpace(MissingTranslationLocale) && MissingTranslationLocale != "None");

        var localeInfo = _localizationData != null 
            ? $" with {_localizationData.LoadedLocales.Count()} locales" 
            : "";

        if (hasActiveFilters)
        {
            StatusMessage = $"Showing {FilteredCount} of {TotalCount} ({TotalCount} total){localeInfo}";
        }
        else
        {
            StatusMessage = $"Showing {FilteredCount} of {TotalCount} entries{localeInfo}";
        }
        
        Debug.WriteLine($"[GridPage] ApplyFilters: FilteredCount={FilteredCount}, HasData={HasData}");
    }

    private bool IsMissingTranslationForLocale(LocalizationEntryViewModel entry, string localeCode)
    {
        return localeCode switch
        {
            "enUS" => string.IsNullOrWhiteSpace(entry.EnUS),
            "enGB" => string.IsNullOrWhiteSpace(entry.EnGB),
            "enTW" => string.IsNullOrWhiteSpace(entry.EnTW),
            "enCN" => string.IsNullOrWhiteSpace(entry.EnCN),
            "deDE" => string.IsNullOrWhiteSpace(entry.DeDE),
            "esES" => string.IsNullOrWhiteSpace(entry.EsES),
            "esMX" => string.IsNullOrWhiteSpace(entry.EsMX),
            "frFR" => string.IsNullOrWhiteSpace(entry.FrFR),
            "itIT" => string.IsNullOrWhiteSpace(entry.ItIT),
            "koKR" => string.IsNullOrWhiteSpace(entry.KoKR),
            "ptBR" => string.IsNullOrWhiteSpace(entry.PtBR),
            "ptPT" => string.IsNullOrWhiteSpace(entry.PtPT),
            "ruRU" => string.IsNullOrWhiteSpace(entry.RuRU),
            "zhCN" => string.IsNullOrWhiteSpace(entry.ZhCN),
            "zhTW" => string.IsNullOrWhiteSpace(entry.ZhTW),
            _ => false
        };
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        ShowOnlyConcatenated = false;
        ShowOnlyStringFormat = false;
        ShowOnlyWithParameters = false;
        MissingTranslationLocale = "None";
    }

    [RelayCommand]
    private async Task ViewDetails(LocalizationEntryViewModel entry)
    {
        if (entry == null) return;

        await Shell.Current.GoToAsync("detail", new Dictionary<string, object>
        {
            { "Entry", entry }
        });
    }

    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private async Task SaveChanges()
    {
        if (string.IsNullOrWhiteSpace(_localizationDirectory))
        {
            await _dialogService.ShowAlertAsync("Error", "Localization directory not set. Cannot save changes.");
            return;
        }

        var modifiedEntries = GetModifiedEntries().ToList();
        if (modifiedEntries.Count == 0)
        {
            await _dialogService.ShowAlertAsync("No Changes", "There are no changes to save.");
            return;
        }

        // Count affected locales
        var affectedLocales = new HashSet<string>();
        foreach (var entry in modifiedEntries)
        {
            var changes = entry.GetChangedTranslations();
            foreach (var locale in changes.Keys)
            {
                affectedLocales.Add(locale);
            }
        }

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Save Changes",
            $"Save {modifiedEntries.Count} modified translation(s) across {affectedLocales.Count} locale file(s)?\n\nBackups will be created automatically.",
            "Save",
            "Cancel");

        if (!confirm)
        {
            return;
        }

        try
        {
            IsSaving = true;
            SaveProgress = 0;
            StatusMessage = "Preparing to save...";

            // Group translations by locale
            var localeTranslations = new Dictionary<string, Dictionary<string, string>>();

            foreach (var entry in modifiedEntries)
            {
                var changedTranslations = entry.GetChangedTranslations();
                
                foreach (var (localeCode, translation) in changedTranslations)
                {
                    if (!localeTranslations.ContainsKey(localeCode))
                    {
                        localeTranslations[localeCode] = new Dictionary<string, string>();
                    }

                    localeTranslations[localeCode][entry.GlueString] = translation;
                }
            }

            // Merge with existing translations to preserve non-modified entries
            if (_localizationData != null)
            {
                foreach (var (localeCode, _) in localeTranslations.ToList())
                {
                    var existingData = _localizationData.GetLocaleData(localeCode);
                    if (existingData != null)
                    {
                        // Add all existing translations that we haven't modified
                        foreach (var (key, value) in existingData)
                        {
                            if (!localeTranslations[localeCode].ContainsKey(key))
                            {
                                localeTranslations[localeCode][key] = value;
                            }
                        }
                    }
                }
            }

            // Save with progress reporting
            var progress = new Progress<SaveProgress>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SaveProgress = p.PercentComplete / 100.0;
                    
                    if (p.Error != null)
                    {
                        StatusMessage = $"Error saving {p.LocaleCode}: {p.Error}";
                    }
                    else if (p.IsComplete)
                    {
                        StatusMessage = $"Successfully saved all changes";
                    }
                    else
                    {
                        StatusMessage = $"Saving {p.LocaleCode}... ({p.ProcessedCount}/{p.TotalCount})";
                    }
                });
            });

            await _fileWriter.SaveMultipleLocaleFilesAsync(
                _localizationDirectory,
                localeTranslations,
                createBackup: true,
                progress: progress);

            // Commit changes to mark them as saved
            foreach (var entry in modifiedEntries)
            {
                entry.CommitChanges();
            }

            UpdateChangeTracking();
            
            await _dialogService.ShowAlertAsync(
                "Save Successful",
                $"Successfully saved {modifiedEntries.Count} translation(s) to {localeTranslations.Count} locale file(s).");
            
            StatusMessage = $"Saved {modifiedEntries.Count} translation(s) to {localeTranslations.Count} file(s)";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GridPage] Error saving changes: {ex.Message}");
            Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
            
            await _dialogService.ShowAlertAsync(
                "Save Failed",
                $"An error occurred while saving:\n\n{ex.Message}");
            
            StatusMessage = $"Error saving: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
            SaveProgress = 0;
        }
    }

    private bool CanSaveChanges() => HasUnsavedChanges && !IsSaving && !IsLoading;

    [RelayCommand(CanExecute = nameof(CanDiscardChanges))]
    private async Task DiscardChanges()
    {
        var modifiedEntries = GetModifiedEntries().ToList();
        
        var confirm = await _dialogService.ShowConfirmationAsync(
            "Discard Changes",
            $"Discard {modifiedEntries.Count} unsaved change(s)?\n\nThis action cannot be undone.",
            "Discard",
            "Cancel");

        if (!confirm)
        {
            return;
        }

        foreach (var entry in modifiedEntries)
        {
            entry.ResetChanges();
        }

        UpdateChangeTracking();
        StatusMessage = $"Discarded {modifiedEntries.Count} unsaved change(s)";
    }

    private bool CanDiscardChanges() => HasUnsavedChanges && !IsSaving && !IsLoading;

    [RelayCommand]
    private async Task Reload()
    {
        if (HasUnsavedChanges)
        {
            var confirm = await _dialogService.ShowConfirmationAsync(
                "Unsaved Changes",
                "You have unsaved changes. Reloading will discard them.\n\nContinue?",
                "Reload",
                "Cancel");

            if (!confirm)
            {
                return;
            }
        }

        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task ExportData()
    {
        await _dialogService.ShowAlertAsync("Export", "Export functionality coming soon!");
    }

    [RelayCommand]
    private void TestEdit()
    {
        // Test command to verify change tracking
        if (Entries.Count > 0)
        {
            var firstEntry = Entries[0];
            Debug.WriteLine($"[GridPage] Test Edit - Before: enUS = '{firstEntry.EnUS}'");
            firstEntry.EnUS = "TEST EDIT - " + DateTime.Now.ToString("HH:mm:ss");
            Debug.WriteLine($"[GridPage] Test Edit - After: enUS = '{firstEntry.EnUS}'");
            Debug.WriteLine($"[GridPage] Test Edit - HasChanges: {firstEntry.HasChanges()}");
        }
        else
        {
            Debug.WriteLine("[GridPage] Test Edit - No entries to edit");
        }
    }

    /// <summary>
    /// Get all entries with unsaved changes
    /// </summary>
    public IEnumerable<LocalizationEntryViewModel> GetModifiedEntries()
    {
        return Entries.Where(e => e.HasChanges());
    }

    /// <summary>
    /// Handle page appearing - warn if trying to navigate away with unsaved changes
    /// </summary>
    public async Task<bool> OnNavigatingAwayAsync()
    {
        if (HasUnsavedChanges)
        {
            var confirm = await _dialogService.ShowConfirmationAsync(
                "Unsaved Changes",
                $"You have {ModifiedEntryCount} unsaved change(s).\n\nLeave without saving?",
                "Leave",
                "Stay");

            return confirm;
        }

        return true;
    }

    /// <summary>
    /// Configure the Google Translate service with an API key
    /// </summary>
    public void ConfigureTranslateService(string apiKey)
    {
        _translateService.Configure(apiKey);
        Debug.WriteLine("[GridPage] Google Translate service configured");
    }

    /// <summary>
    /// Check if the translation service is configured
    /// </summary>
    public bool IsTranslateServiceConfigured => _translateService.IsConfigured;

    [RelayCommand(CanExecute = nameof(CanAutoTranslate))]
    private async Task AutoTranslate()
    {
        if (!_translateService.IsConfigured)
        {
            // Try to configure from environment variable
            if (!_translateService.TryConfigureFromEnvironment())
            {
                await _dialogService.ShowAlertAsync("Not Configured", 
                    "Google Translate service not configured.\n\n" +
                    "Please set the GOOGLE_APPLICATION_CREDENTIALS environment variable to the path of your service account JSON key file and restart the application.");
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(_localizationDirectory))
        {
            await _dialogService.ShowAlertAsync("Error", 
                "Localization directory not set. Cannot save translations.");
            return;
        }

        // Get entries that have enUS text but may be missing GT translations
        var entriesToTranslate = Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.EnUS))
            .ToList();

        if (entriesToTranslate.Count == 0)
        {
            await _dialogService.ShowAlertAsync("Nothing to Translate", 
                "No entries with enUS text found.");
            return;
        }

        // Count how many translations are actually needed per locale
        var baseLocales = LocaleDefinitions.GetGTBaseLocales().ToList();
        var translationsNeeded = new Dictionary<string, int>();
        
        foreach (var baseLocale in baseLocales)
        {
            var missingCount = entriesToTranslate.Count(e => !HasGTTranslationForBase(e, baseLocale));
            if (missingCount > 0)
            {
                translationsNeeded[baseLocale] = missingCount;
            }
        }

        if (translationsNeeded.Count == 0)
        {
            await _dialogService.ShowAlertAsync("All Translated", 
                "All entries already have GT translations for all locales.");
            return;
        }

        var totalMissing = translationsNeeded.Values.Sum();
        var localeList = string.Join("\n", translationsNeeded.Select(kvp => $"  • {kvp.Key}: {kvp.Value} missing"));

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Auto-Translate",
            $"Translate missing entries to {translationsNeeded.Count} locale(s)?\n\n{localeList}\n\n" +
            $"Total API calls: ~{totalMissing}\n\n" +
            "This will add translations to the -GT.lua files only.",
            "Translate",
            "Cancel");

        if (!confirm) return;

        try
        {
            IsTranslating = true;
            TranslationProgress = 0;
            var processedLocales = 0;

            foreach (var baseLocale in baseLocales)
            {
                if (!translationsNeeded.ContainsKey(baseLocale))
                {
                    continue;
                }

                var googleLangCode = LocaleDefinitions.GetGoogleLanguageCode(baseLocale);
                if (string.IsNullOrEmpty(googleLangCode))
                {
                    Debug.WriteLine($"[GridPage] No Google language code for {baseLocale}");
                    continue;
                }

                // Filter to entries missing GT for this locale
                var missingEntries = entriesToTranslate
                    .Where(e => !HasGTTranslationForBase(e, baseLocale))
                    .ToList();

                if (missingEntries.Count == 0) continue;

                TranslationStatus = $"Translating to {baseLocale} ({missingEntries.Count} entries)...";
                Debug.WriteLine($"[GridPage] Translating {missingEntries.Count} entries to {baseLocale}");

                var textsToTranslate = missingEntries
                    .Select(e => e.EnUS)
                    .Distinct()
                    .ToList();

                var translations = await _translateService.TranslateBatchAsync(
                    textsToTranslate,
                    googleLangCode,
                    new Progress<TranslationProgress>(p =>
                    {
                        var localeProgress = (double)p.ProcessedCount / p.TotalCount;
                        var overallProgress = (processedLocales + localeProgress) / baseLocales.Count;
                        TranslationProgress = overallProgress;
                    }));

                // Save translations to GT file
                await SaveGTTranslationsAsync(baseLocale, missingEntries, translations);
                
                processedLocales++;
                TranslationProgress = (double)processedLocales / baseLocales.Count;
            }

            TranslationStatus = "Translation complete!";
            await _dialogService.ShowAlertAsync("Complete", 
                "Auto-translation completed. GT files have been updated.\n\n" +
                "Reload to see updated GT status in the grid.");

            // Optionally reload data to reflect new GT status
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GridPage] Auto-translate error: {ex.Message}");
            await _dialogService.ShowAlertAsync("Translation Error", 
                $"An error occurred during translation:\n\n{ex.Message}");
        }
        finally
        {
            IsTranslating = false;
            TranslationProgress = 0;
            TranslationStatus = string.Empty;
        }
    }

    private bool CanAutoTranslate() => !IsTranslating && !IsSaving && !IsLoading && Entries.Count > 0;

    private bool HasGTTranslationForBase(LocalizationEntryViewModel entry, string baseLocale)
    {
        return baseLocale switch
        {
            "de" => entry.HasDeGT,
            "es" => entry.HasEsGT,
            "fr" => entry.HasFrGT,
            "it" => entry.HasItGT,
            "ko" => entry.HasKoGT,
            "pt" => entry.HasPtGT,
            "ru" => entry.HasRuGT,
            "zh" => entry.HasZhCNGT,
            "zh-TW" => entry.HasZhTWGT,
            _ => false
        };
    }

    private async Task SaveGTTranslationsAsync(
        string baseLocale,
        List<LocalizationEntryViewModel> entries,
        Dictionary<string, string> translations)
    {
        if (string.IsNullOrWhiteSpace(_localizationDirectory)) return;

        // Get the GT filename based on the base locale using centralized helper
        var gtFileName = LocaleDefinitions.GetGTFileName(baseLocale);
        var gtFilePath = Path.Combine(_localizationDirectory, gtFileName);

        // Load existing GT translations if file exists
        var existingTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(gtFilePath))
        {
            try
            {
                existingTranslations = await _parserService.ParseLocaleTranslationsAsync(gtFilePath);
                Debug.WriteLine($"[GridPage] Loaded {existingTranslations.Count} existing translations from {gtFileName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GridPage] Error loading existing GT file: {ex.Message}");
            }
        }

        // Get valid glue string keys from the parse result
        var validKeys = _parseResult?.GlueStrings.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase) 
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Remove entries that no longer exist in enUS (orphaned translations)
        var orphanedKeys = existingTranslations.Keys
            .Where(k => !validKeys.Contains(k))
            .ToList();
        
        foreach (var orphanedKey in orphanedKeys)
        {
            existingTranslations.Remove(orphanedKey);
        }
        
        if (orphanedKeys.Count > 0)
        {
            Debug.WriteLine($"[GridPage] Removed {orphanedKeys.Count} orphaned translations from {gtFileName}");
        }

        // Merge new translations (don't overwrite existing)
        var addedCount = 0;
        foreach (var entry in entries)
        {
            // Skip if already exists
            if (existingTranslations.ContainsKey(entry.GlueString))
            {
                continue;
            }

            if (translations.TryGetValue(entry.EnUS, out var translated))
            {
                existingTranslations[entry.GlueString] = translated;
                addedCount++;
            }
        }

        if (addedCount == 0 && orphanedKeys.Count == 0)
        {
            Debug.WriteLine($"[GridPage] No changes to make for {baseLocale}");
            return;
        }

        // Write the GT file with a special format that includes the base locale code
        await WriteGTFileAsync(gtFilePath, baseLocale, existingTranslations);
        Debug.WriteLine($"[GridPage] Saved gtFileName: added {addedCount}, removed {orphanedKeys.Count} orphaned");
    }

    private async Task WriteGTFileAsync(string filePath, string baseLocale, Dictionary<string, string> translations)
    {
        // Get all WoW locales that share this base locale (e.g., "pt" -> ["ptBR", "ptPT"])
        var wowLocaleCodes = LocaleDefinitions.GetLocalesForBase(baseLocale)
            .Where(l => !LocaleDefinitions.IsEnglishLocale(l))
            .ToList();
        
        if (wowLocaleCodes.Count == 0)
        {
            Debug.WriteLine($"[GridPage] No WoW locales found for base locale: {baseLocale}");
            return;
        }

        var lines = new List<string>
        {
            "local _, TRB = ...",
            "",
            "local locale = GetLocale()",
            ""
        };

        // Build the locale condition using "or" for multiple locales
        // e.g., 'if locale == "esES" or locale == "esMX" then'
        var localeConditions = wowLocaleCodes.Select(l => $"locale == \"{l}\"");
        var combinedCondition = string.Join(" or ", localeConditions);
        
        lines.Add($"if {combinedCondition} then");
        lines.Add("    local L = TRB.Localization");
        lines.Add("");

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

        await File.WriteAllLinesAsync(filePath, lines);
    }

    private static string EscapeString(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    [RelayCommand]
    private async Task TranslateLocale(string baseLocale)
    {
        Debug.WriteLine($"[GridPage] TranslateLocale called for: {baseLocale}");

        if (!_translateService.IsConfigured)
        {
            // Try to configure from environment variable
            if (!_translateService.TryConfigureFromEnvironment())
            {
                await _dialogService.ShowAlertAsync("Not Configured", 
                    "Google Translate service not configured.\n\n" +
                    "Please set the GOOGLE_APPLICATION_CREDENTIALS environment variable to the path of your service account JSON key file and restart the application.");
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(_localizationDirectory))
        {
            await _dialogService.ShowAlertAsync("Error", 
                "Localization directory not set. Cannot save translations.");
            return;
        }

        // Get entries that have enUS text
        var entriesToTranslate = Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.EnUS))
            .ToList();

        if (entriesToTranslate.Count == 0)
        {
            await _dialogService.ShowAlertAsync("Nothing to Translate", 
                "No entries with enUS text found.");
            return;
        }

        // Filter to entries missing GT for this locale
        var missingEntries = entriesToTranslate
            .Where(e => !HasGTTranslationForBase(e, baseLocale))
            .ToList();

        if (missingEntries.Count == 0)
        {
            await _dialogService.ShowAlertAsync("All Translated", 
                $"All entries already have {baseLocale}-GT translations.");
            return;
        }

        var googleLangCode = LocaleDefinitions.GetGoogleLanguageCode(baseLocale);
        if (string.IsNullOrEmpty(googleLangCode))
        {
            await _dialogService.ShowAlertAsync("Error", 
                $"No Google language code found for {baseLocale}.");
            return;
        }

        var confirm = await _dialogService.ShowConfirmationAsync(
            $"Translate to {baseLocale}-GT",
            $"Translate {missingEntries.Count} missing entries to {baseLocale}?\n\n" +
            $"Google language code: {googleLangCode}\n\n" +
            "This will add translations to the -GT.lua file only.",
            "Translate",
            "Cancel");

        if (!confirm) return;

        try
        {
            IsTranslating = true;
            TranslationProgress = 0;
            TranslationStatus = $"Translating to {baseLocale}...";

            var textsToTranslate = missingEntries
                .Select(e => e.EnUS)
                .Distinct()
                .ToList();

            Debug.WriteLine($"[GridPage] Translating {textsToTranslate.Count} unique texts to {googleLangCode}");

            var translations = await _translateService.TranslateBatchAsync(
                textsToTranslate,
                googleLangCode,
                new Progress<TranslationProgress>(p =>
                {
                    TranslationProgress = (double)p.ProcessedCount / p.TotalCount;
                    TranslationStatus = $"Translating to {baseLocale}: {p.ProcessedCount}/{p.TotalCount}";
                }));

            Debug.WriteLine($"[GridPage] Received {translations.Count} translations");

            // Save translations to GT file
            await SaveGTTranslationsAsync(baseLocale, missingEntries, translations);

            TranslationStatus = "Translation complete!";
            await _dialogService.ShowAlertAsync("Complete", 
                $"Translation to {baseLocale}-GT completed.\n\n" +
                $"Added {translations.Count} translations.\n\n" +
                "Reloading data to update GT status...");

            // Reload data to reflect new GT status
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GridPage] Translation error: {ex.Message}");
            Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
            await _dialogService.ShowAlertAsync("Translation Error", 
                $"An error occurred during translation:\n\n{ex.Message}");
        }
        finally
        {
            IsTranslating = false;
            TranslationProgress = 0;
            TranslationStatus = string.Empty;
        }
    }
}
