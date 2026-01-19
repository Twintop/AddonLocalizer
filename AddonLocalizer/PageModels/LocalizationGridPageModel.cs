using System.Collections.ObjectModel;
using AddonLocalizer.Core.Interfaces;
using AddonLocalizer.Core.Models;
using AddonLocalizer.Core.Services;
using AddonLocalizer.Models;
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

    [ObservableProperty]
    private int _orphanedEntryCount;

    [ObservableProperty]
    private bool _hasOrphanedEntries;

    [ObservableProperty]
    private List<string> _orphanedGlueStrings = [];

    [ObservableProperty]
    private Dictionary<string, List<string>> _orphanedEntriesByFile = [];

    [ObservableProperty]
    private int _orphanedFileCount;

    [ObservableProperty]
    private bool _hasDuplicateEntries;

    [ObservableProperty]
    private int _duplicateEntryCount;

    [ObservableProperty]
    private Dictionary<string, List<DuplicateEntry>> _duplicateEntriesByLocale = [];

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

                    // Check for duplicates
                    if (_localizationData.HasDuplicates)
                    {
                        DuplicateEntriesByLocale = _localizationData.GetAllDuplicates();
                        DuplicateEntryCount = _localizationData.TotalDuplicateCount;
                        HasDuplicateEntries = true;
                        Debug.WriteLine($"[GridPage] Found {DuplicateEntryCount} duplicate entries across {DuplicateEntriesByLocale.Count} locales");
                    }
                    else
                    {
                        DuplicateEntriesByLocale = [];
                        DuplicateEntryCount = 0;
                        HasDuplicateEntries = false;
                    }
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

            // Calculate orphaned entries (in localization files but not in code)
            var codeGlueStrings = _parseResult.GlueStrings.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var (orphanedStrings, orphanedByFile) = await Task.Run(() =>
            {
                if (_localizationData == null || _parseResult == null)
                    return (new List<string>(), new Dictionary<string, List<string>>());

                // Get all glue strings from localization files
                var localizationGlueStrings = _localizationData.AllGlueStrings;

                // Find orphaned entries: in localization files but not in code
                var orphaned = localizationGlueStrings
                    .Where(gs => !codeGlueStrings.Contains(gs))
                    .OrderBy(gs => gs)
                    .ToList();

                // Get detailed breakdown by file (including GT files)
                var byFile = _localizationData.GetOrphanedEntriesByFile(codeGlueStrings);

                return (orphaned, byFile);
            });

            Debug.WriteLine($"[GridPage] Found {orphanedStrings.Count} orphaned entries");

            // Update UI on main thread - use assignment instead of Clear/Add
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    StatusMessage = $"Loaded {entries.Count} entries";

                    // Create new collection instead of modifying existing
                    Entries = new ObservableCollection<LocalizationEntryViewModel>(entries);
                    TotalCount = entries.Count;

                    // Update orphaned entry tracking
                    OrphanedGlueStrings = orphanedStrings;
                    OrphanedEntryCount = orphanedStrings.Count;
                    HasOrphanedEntries = orphanedStrings.Count > 0;

                    OrphanedEntriesByFile = orphanedByFile;
                    OrphanedFileCount = orphanedByFile.Count;

                    ApplyFilters();

                    HasData = FilteredEntries.Count > 0;

                    Debug.WriteLine($"[GridPage] HasData set to: {HasData}, FilteredCount: {FilteredCount}");

                    var localeInfo = _localizationData != null
                        ? $" with {_localizationData.LoadedLocales.Count()} locales"
                        : "";
                    var orphanedInfo = HasOrphanedEntries
                        ? $" | ?? {OrphanedEntryCount} orphaned"
                        : "";
                    var duplicateInfo = HasDuplicateEntries
                        ? $" | ?? {DuplicateEntryCount} duplicates"
                        : "";
                    StatusMessage = HasData
                        ? $"Showing {FilteredCount} of {TotalCount} entries{localeInfo}{orphanedInfo}{duplicateInfo}"
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

            // Show duplicate entries alert after UI is loaded
            if (HasDuplicateEntries)
            {
                await ShowDuplicateEntriesAlertAsync();
            }
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

    private async Task ShowDuplicateEntriesAlertAsync()
    {
        if (!HasDuplicateEntries || DuplicateEntriesByLocale.Count == 0)
            return;

        // Build a summary message
        var summary = new System.Text.StringBuilder();
        summary.AppendLine("Duplicate localization entries were found. The last value for each key will be used.");
        summary.AppendLine();

        foreach (var (locale, duplicates) in DuplicateEntriesByLocale.OrderBy(kvp => kvp.Key))
        {
            summary.AppendLine($"?? {locale}.lua ({duplicates.Count} duplicate{(duplicates.Count > 1 ? "s" : "")}):");

            // Show first few duplicates (limit to avoid too long message)
            var displayLimit = Math.Min(duplicates.Count, 5);
            foreach (var dup in duplicates.Take(displayLimit))
            {
                summary.AppendLine($"  • {dup.Key} ({dup.OccurrenceCount} occurrences)");
            }

            if (duplicates.Count > displayLimit)
            {
                summary.AppendLine($"  ... and {duplicates.Count - displayLimit} more");
            }
            summary.AppendLine();
        }

        summary.AppendLine("Would you like to clean up these duplicates now?");
        summary.AppendLine("(This will save the files with duplicates removed, keeping the last value for each key)");

        var shouldCleanup = await _dialogService.ShowConfirmationAsync(
            "?? Duplicate Entries Found",
            summary.ToString(),
            "Clean Up Duplicates",
            "Skip for Now");

        if (shouldCleanup)
        {
            await CleanupDuplicateEntriesAsync();
        }
    }

    [RelayCommand]
    private async Task CleanupDuplicateEntriesAsync()
    {
        if (!HasDuplicateEntries || string.IsNullOrEmpty(_localizationDirectory))
        {
            await _dialogService.ShowAlertAsync("No Duplicates", "No duplicate entries to clean up.");
            return;
        }

        try
        {
            IsSaving = true;
            SaveProgress = 0;
            StatusMessage = "Cleaning up duplicate entries...";
            Debug.WriteLine("[GridPage] Starting duplicate cleanup...");
            Debug.WriteLine($"[GridPage] Localization directory: {_localizationDirectory}");
            Debug.WriteLine($"[GridPage] Locales with duplicates: {string.Join(", ", DuplicateEntriesByLocale.Keys)}");

            var totalLocales = DuplicateEntriesByLocale.Count;
            var processedLocales = 0;
            var cleanedLocales = new List<string>();

            foreach (var (localeCode, duplicates) in DuplicateEntriesByLocale)
            {
                try
                {
                    StatusMessage = $"Cleaning up {localeCode}...";
                    Debug.WriteLine($"[GridPage] Processing {localeCode} with {duplicates.Count} duplicate keys:");
                    foreach (var dup in duplicates)
                    {
                        Debug.WriteLine($"[GridPage]   - {dup.Key}: {dup.OccurrenceCount} occurrences, final value = '{dup.FinalValue}'");
                    }

                    // Get the current translations (already de-duplicated with last value winning)
                    var translations = _localizationData?.GetLocaleData(localeCode);
                    if (translations == null || translations.Count == 0)
                    {
                        Debug.WriteLine($"[GridPage] No translations found for {localeCode}, skipping");
                        continue;
                    }

                    Debug.WriteLine($"[GridPage] Retrieved {translations.Count} translations for {localeCode}");

                    // Log sample translations to verify values
                    foreach (var dup in duplicates.Take(3))
                    {
                        if (translations.TryGetValue(dup.Key, out var value))
                        {
                            Debug.WriteLine($"[GridPage]   Translation for '{dup.Key}' = '{value}'");
                        }
                        else
                        {
                            Debug.WriteLine($"[GridPage]   WARNING: Key '{dup.Key}' not found in translations!");
                        }
                    }

                    // Save the file (this will write without duplicates)
                    Debug.WriteLine($"[GridPage] Calling SaveLocaleFileAsync for {localeCode}...");
                    await _fileWriter.SaveLocaleFileAsync(
                        _localizationDirectory,
                        localeCode,
                        translations,
                        createBackup: true);

                    cleanedLocales.Add(localeCode);
                    Debug.WriteLine($"[GridPage] Successfully cleaned up duplicates in {localeCode}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GridPage] Error cleaning up {localeCode}: {ex.Message}");
                    Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
                    await _dialogService.ShowAlertAsync("Error", $"Failed to clean up {localeCode}: {ex.Message}");
                }

                processedLocales++;
                SaveProgress = (double)processedLocales / totalLocales;
            }

            // Clear duplicate tracking
            DuplicateEntriesByLocale = [];
            DuplicateEntryCount = 0;
            HasDuplicateEntries = false;

            // Reload data to reflect changes
            await LoadDataAsync();

            var message = cleanedLocales.Count > 0
                ? $"Successfully cleaned up duplicates in {cleanedLocales.Count} locale{(cleanedLocales.Count > 1 ? "s" : "")}: {string.Join(", ", cleanedLocales)}\n\nBackups were created for each file."
                : "No locales needed cleanup.";

            await _dialogService.ShowAlertAsync("Cleanup Complete", message);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GridPage] Error during duplicate cleanup: {ex.Message}");
            Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
            await _dialogService.ShowAlertAsync("Error", $"An error occurred during cleanup: {ex.Message}");
        }
        finally
        {
            IsSaving = false;
            SaveProgress = 0;
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

    partial void OnHasOrphanedEntriesChanged(bool value)
    {
        ShowOrphanedEntriesCommand.NotifyCanExecuteChanged();
        CleanupOrphanedEntriesCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        ShowOrphanedEntriesCommand.NotifyCanExecuteChanged();
        CleanupOrphanedEntriesCommand.NotifyCanExecuteChanged();
        AutoTranslateCommand.NotifyCanExecuteChanged();
        TranslateLocaleCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSavingChanged(bool value)
    {
        CleanupOrphanedEntriesCommand.NotifyCanExecuteChanged();
        AutoTranslateCommand.NotifyCanExecuteChanged();
        TranslateLocaleCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsTranslatingChanged(bool value)
    {
        CleanupOrphanedEntriesCommand.NotifyCanExecuteChanged();
        AutoTranslateCommand.NotifyCanExecuteChanged();
        TranslateLocaleCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasUnsavedChangesChanged(bool value)
    {
        CleanupOrphanedEntriesCommand.NotifyCanExecuteChanged();
    }

    partial void OnEntriesChanged(ObservableCollection<LocalizationEntryViewModel> value)
    {
        AutoTranslateCommand.NotifyCanExecuteChanged();
        TranslateLocaleCommand.NotifyCanExecuteChanged();
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
                e.GlueString.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(e.EnUS) && e.EnUS.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.EnGB) && e.EnGB.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.EnTW) && e.EnTW.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.EnCN) && e.EnCN.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.DeDE) && e.DeDE.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.EsES) && e.EsES.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.EsMX) && e.EsMX.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.FrFR) && e.FrFR.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.ItIT) && e.ItIT.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.KoKR) && e.KoKR.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.PtBR) && e.PtBR.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.PtPT) && e.PtPT.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.RuRU) && e.RuRU.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.ZhCN) && e.ZhCN.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.ZhTW) && e.ZhTW.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
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

        var orphanedInfo = HasOrphanedEntries
            ? $" | ?? {OrphanedEntryCount} orphaned"
            : "";

        if (hasActiveFilters)
        {
            StatusMessage = $"Showing {FilteredCount} of {TotalCount} ({TotalCount} total){localeInfo}{orphanedInfo}";
        }
        else
        {
            StatusMessage = $"Showing {FilteredCount} of {TotalCount} entries{localeInfo}{orphanedInfo}";
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

        // Check for entries where enUS has changed
        var entriesWithEnUSChanges = modifiedEntries
            .Where(e => e.HasEnUSChanged())
            .ToList();

        Debug.WriteLine($"[GridPage] Entries with enUS changes: {entriesWithEnUSChanges.Count}");
        foreach (var entry in entriesWithEnUSChanges)
        {
            Debug.WriteLine($"[GridPage]   {entry.GlueString}: original='{entry.GetOriginalEnUS()}', current='{entry.EnUS}'");
            Debug.WriteLine($"[GridPage]   HasNonEnUSManualTranslations: {entry.HasNonEnUSManualTranslations()}");
            if (entry.HasNonEnUSManualTranslations())
            {
                var locales = entry.GetNonEnUSLocalesWithTranslations();
                Debug.WriteLine($"[GridPage]   Non-enUS locales with translations: {string.Join(", ", locales)}");
            }
        }

        var entriesToRetranslate = new List<LocalizationEntryViewModel>();
        var entriesToClearManualTranslations = new List<LocalizationEntryViewModel>();

        if (entriesWithEnUSChanges.Count > 0)
        {
            // Check which have non-enUS manual translations (includes enGB, enTW, enCN and all other languages)
            var entriesWithManualTranslations = entriesWithEnUSChanges
                .Where(e => e.HasNonEnUSManualTranslations())
                .ToList();

            Debug.WriteLine($"[GridPage] Entries with manual translations: {entriesWithManualTranslations.Count}");

            if (entriesWithManualTranslations.Count > 0)
            {
                // Build details about affected entries
                var details = new System.Text.StringBuilder();
                details.AppendLine($"{entriesWithManualTranslations.Count} entry/entries have enUS changes with existing translations in other locales:");
                details.AppendLine();

                foreach (var entry in entriesWithManualTranslations.Take(5))
                {
                    var locales = entry.GetNonEnUSLocalesWithTranslations();
                    details.AppendLine($"• {entry.GlueString}");
                    details.AppendLine($"  Old: \"{entry.GetOriginalEnUS()}\"");
                    details.AppendLine($"  New: \"{entry.EnUS}\"");
                    details.AppendLine($"  Has translations: {string.Join(", ", locales)}");
                    details.AppendLine();
                }

                if (entriesWithManualTranslations.Count > 5)
                {
                    details.AppendLine($"... and {entriesWithManualTranslations.Count - 5} more");
                    details.AppendLine();
                }

                details.AppendLine("These translations may now be out of date.");
                details.AppendLine("Would you like to clear them?");

                var clearManualTranslations = await _dialogService.ShowConfirmationAsync(
                    "?? enUS Changed - Clear Other Translations?",
                    details.ToString(),
                    "Clear Translations",
                    "Keep Translations");

                if (clearManualTranslations)
                {
                    entriesToClearManualTranslations.AddRange(entriesWithManualTranslations);
                }
            }

            // Always mark entries for GT re-translation when enUS changes
            entriesToRetranslate.AddRange(entriesWithEnUSChanges);

            // Mark them so we know to re-translate after save
            foreach (var entry in entriesWithEnUSChanges)
            {
                entry.MarkEnUSChanged();
            }
        }

        // Apply translation clearing if user confirmed
        foreach (var entry in entriesToClearManualTranslations)
        {
            Debug.WriteLine($"[GridPage] Clearing non-enUS translations for: {entry.GlueString}");
            Debug.WriteLine($"[GridPage]   Before: EnGB='{entry.EnGB}', EnTW='{entry.EnTW}', EnCN='{entry.EnCN}', DeDE='{entry.DeDE}', EsES='{entry.EsES}', FrFR='{entry.FrFR}'");
            entry.ClearNonEnUSTranslations();
            Debug.WriteLine($"[GridPage]   After: EnGB='{entry.EnGB}', EnTW='{entry.EnTW}', EnCN='{entry.EnCN}', DeDE='{entry.DeDE}', EsES='{entry.EsES}', FrFR='{entry.FrFR}'");
        }

        // Count affected locales (recalculate after potential clearing)
        modifiedEntries = GetModifiedEntries().ToList(); // Refresh list after clearing
        Debug.WriteLine($"[GridPage] After clearing, modified entries count: {modifiedEntries.Count}");

        var affectedLocales = new HashSet<string>();
        foreach (var entry in modifiedEntries)
        {
            var changes = entry.GetChangedTranslations();
            Debug.WriteLine($"[GridPage] Entry '{entry.GlueString}' has {changes.Count} changed translations:");
            foreach (var (locale, value) in changes)
            {
                Debug.WriteLine($"[GridPage]   {locale} = '{(string.IsNullOrEmpty(value) ? "(empty)" : value)}'");
                affectedLocales.Add(locale);
            }
        }
        Debug.WriteLine($"[GridPage] Total affected locales: {affectedLocales.Count} - {string.Join(", ", affectedLocales)}");

        var confirmMessage = $"Save {modifiedEntries.Count} modified translation(s) across {affectedLocales.Count} locale file(s)?";
        
        if (entriesToRetranslate.Count > 0)
        {
            confirmMessage += $"\n\n{entriesToRetranslate.Count} entry/entries will be re-translated to all GT files after saving.";
        }
        
        confirmMessage += "\n\nBackups will be created automatically.";

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Save Changes",
            confirmMessage,
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
                        Debug.WriteLine($"[GridPage] Merging {localeCode}: {existingData.Count} existing entries");
                        var addedFromExisting = 0;
                        // Add all existing translations that we haven't modified
                        foreach (var (key, value) in existingData)
                        {
                            if (!localeTranslations[localeCode].ContainsKey(key))
                            {
                                localeTranslations[localeCode][key] = value;
                                addedFromExisting++;
                            }
                        }
                        Debug.WriteLine($"[GridPage]   Added {addedFromExisting} from existing, total now: {localeTranslations[localeCode].Count}");
                        
                        // Log any entries with empty values (these should be removals)
                        var emptyEntries = localeTranslations[localeCode].Where(kvp => string.IsNullOrEmpty(kvp.Value)).ToList();
                        if (emptyEntries.Count > 0)
                        {
                            Debug.WriteLine($"[GridPage]   Entries to REMOVE (empty values): {string.Join(", ", emptyEntries.Select(e => e.Key))}");
                        }
                    }
                }
            }

            // Log what we're about to save
            Debug.WriteLine($"[GridPage] About to save {localeTranslations.Count} locale files:");
            foreach (var (localeCode, translations) in localeTranslations)
            {
                var emptyCount = translations.Count(kvp => string.IsNullOrEmpty(kvp.Value));
                var nonEmptyCount = translations.Count - emptyCount;
                Debug.WriteLine($"[GridPage]   {localeCode}: {nonEmptyCount} entries, {emptyCount} to remove");
                
                // Log entries being removed
                foreach (var (key, value) in translations.Where(kvp => string.IsNullOrEmpty(kvp.Value)))
                {
                    Debug.WriteLine($"[GridPage]     REMOVING: {key}");
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
            
            // Check if we need to re-translate GT files
            if (entriesToRetranslate.Count > 0)
            {
                await _dialogService.ShowAlertAsync(
                    "Save Successful",
                    $"Successfully saved {modifiedEntries.Count} translation(s) to {localeTranslations.Count} locale file(s).\n\n" +
                    $"Now updating {entriesToRetranslate.Count} GT translation(s)...");

                await RetranslateEntriesAsync(entriesToRetranslate);
            }
            else
            {
                await _dialogService.ShowAlertAsync(
                    "Save Successful",
                    $"Successfully saved {modifiedEntries.Count} translation(s) to {localeTranslations.Count} locale file(s).");
            }
            
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

    [RelayCommand(CanExecute = nameof(CanShowOrphanedEntries))]
    private async Task ShowOrphanedEntries()
    {
        if (!HasOrphanedEntries || OrphanedGlueStrings.Count == 0)
        {
            await _dialogService.ShowAlertAsync("No Orphaned Entries", 
                "There are no orphaned entries in the localization files.");
            return;
        }

        // Build file breakdown summary
        var fileBreakdown = new List<string>();
        foreach (var (fileName, orphanedKeys) in OrphanedEntriesByFile.OrderBy(kvp => kvp.Key))
        {
            fileBreakdown.Add($"  - {fileName}: {orphanedKeys.Count} orphaned");
        }
        var fileSummary = string.Join("\n", fileBreakdown);

        // Show first 15 entries as preview
        var previewCount = Math.Min(15, OrphanedGlueStrings.Count);
        var preview = string.Join("\n", OrphanedGlueStrings.Take(previewCount).Select(s => $"  - {s}"));
        var moreText = OrphanedGlueStrings.Count > 15 
            ? $"\n  ... and {OrphanedGlueStrings.Count - 15} more" 
            : "";

        var cleanupPrompt = "\n\nUse the 'Cleanup' button to remove orphaned entries from all affected files.";

        await _dialogService.ShowAlertAsync(
            $"Orphaned Entries ({OrphanedEntryCount} in {OrphanedFileCount} files)",
            $"These glue strings exist in localization files but are not used in the code.\n\n" +
            $"Affected files:\n{fileSummary}\n\n" +
            $"Orphaned keys:\n{preview}{moreText}" +
            cleanupPrompt);
    }

    private bool CanShowOrphanedEntries() => HasOrphanedEntries && !IsLoading;

    [RelayCommand(CanExecute = nameof(CanCleanupOrphanedEntries))]
    private async Task CleanupOrphanedEntries()
    {
        if (HasUnsavedChanges)
        {
            await _dialogService.ShowAlertAsync("Unsaved Changes",
                $"You have {ModifiedEntryCount} unsaved change(s).\n\n" +
                "Please save or discard your changes before cleaning up orphaned entries.");
            return;
        }

        if (!HasOrphanedEntries || OrphanedEntriesByFile.Count == 0)
        {
            await _dialogService.ShowAlertAsync("No Orphaned Entries", 
                "There are no orphaned entries to clean up.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_localizationDirectory))
        {
            await _dialogService.ShowAlertAsync("Error", 
                "Localization directory not set. Cannot clean up files.");
            return;
        }

        // Build confirmation message with file breakdown
        var fileBreakdown = new List<string>();
        var totalOrphaned = 0;
        foreach (var (fileName, orphanedKeys) in OrphanedEntriesByFile.OrderBy(kvp => kvp.Key))
        {
            fileBreakdown.Add($"  - {fileName}: {orphanedKeys.Count} entries");
            totalOrphaned += orphanedKeys.Count;
        }
        var fileSummary = string.Join("\n", fileBreakdown);

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Cleanup Orphaned Entries",
            $"Remove {totalOrphaned} orphaned entries from {OrphanedFileCount} files?\n\n" +
            $"Files to update:\n{fileSummary}\n\n" +
            "Backups will be created automatically.\n\n" +
            "This action cannot be undone (except by restoring backups).",
            "Cleanup",
            "Cancel");

        if (!confirm) return;

        try
        {
            IsSaving = true;
            SaveProgress = 0;
            StatusMessage = "Cleaning up orphaned entries...";

            var validKeys = _parseResult?.GlueStrings.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase) 
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var processedFiles = 0;
            var totalFiles = OrphanedEntriesByFile.Count;
            var totalRemoved = 0;

            // Process regular locale files
            foreach (var localeCode in _localizationData?.LoadedLocales.ToList() ?? [])
            {
                var orphanedKeys = _localizationData?.GetOrphanedKeysForLocale(localeCode, validKeys) ?? [];
                if (orphanedKeys.Count == 0) continue;

                StatusMessage = $"Cleaning up {localeCode}.lua...";
                
                // Get existing translations and filter out orphaned ones
                var existingData = _localizationData?.GetLocaleData(localeCode);
                if (existingData != null)
                {
                    var cleanedData = existingData
                        .Where(kvp => validKeys.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    // Save the cleaned file
                    await _fileWriter.SaveLocaleFileAsync(
                        _localizationDirectory,
                        localeCode,
                        cleanedData,
                        createBackup: true);

                    totalRemoved += orphanedKeys.Count;
                    Debug.WriteLine($"[GridPage] Cleaned {orphanedKeys.Count} orphaned entries from {localeCode}.lua");
                }

                processedFiles++;
                SaveProgress = (double)processedFiles / totalFiles;
            }

            // Process GT files
            foreach (var baseLocale in _localizationData?.LoadedGTLocales.ToList() ?? [])
            {
                var orphanedKeys = _localizationData?.GetOrphanedKeysForGTLocale(baseLocale, validKeys) ?? [];
                if (orphanedKeys.Count == 0) continue;

                var gtFileName = LocaleDefinitions.GetGTFileName(baseLocale);
                StatusMessage = $"Cleaning up {gtFileName}...";

                // Get existing GT translations and filter out orphaned ones
                var existingData = _localizationData?.GetGTLocaleData(baseLocale);
                if (existingData != null)
                {
                    var cleanedData = existingData
                        .Where(kvp => validKeys.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    // Save the cleaned GT file
                    await WriteGTFileAsync(
                        Path.Combine(_localizationDirectory, gtFileName),
                        baseLocale,
                        cleanedData);

                    totalRemoved += orphanedKeys.Count;
                    Debug.WriteLine($"[GridPage] Cleaned {orphanedKeys.Count} orphaned entries from {gtFileName}");
                }

                processedFiles++;
                SaveProgress = (double)processedFiles / totalFiles;
            }

            StatusMessage = "Cleanup complete!";
            await _dialogService.ShowAlertAsync(
                "Cleanup Complete",
                $"Successfully removed {totalRemoved} orphaned entries from {processedFiles} files.\n\n" +
                "Reloading data...");
            
            // Optionally, reload data to refresh the UI
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GridPage] Cleanup error: {ex.Message}");
            Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
            
            await _dialogService.ShowAlertAsync(
                "Cleanup Failed",
                $"An error occurred during cleanup:\n\n{ex.Message}");
            
            StatusMessage = $"Error during cleanup: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
            SaveProgress = 0;
        }
    }

    private bool CanCleanupOrphanedEntries() => HasOrphanedEntries && !IsSaving && !IsLoading && !IsTranslating && !HasUnsavedChanges;

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
        Debug.WriteLine($"[GridPage] AutoTranslate called - IsConfigured: {_translateService.IsConfigured}");
        Debug.WriteLine($"[GridPage] AutoTranslate - Entries.Count: {Entries.Count}, IsTranslating: {IsTranslating}, IsSaving: {IsSaving}, IsLoading: {IsLoading}");
        
        if (!_translateService.IsConfigured)
        {
            // Try to configure from environment variable
            Debug.WriteLine("[GridPage] AutoTranslate - trying to configure from environment");
            if (!_translateService.TryConfigureFromEnvironment())
            {
                Debug.WriteLine("[GridPage] AutoTranslate - configuration failed");
                await _dialogService.ShowAlertAsync("Not Configured", 
                    "Google Translate service not configured.\n\n" +
                    "Please set the GOOGLE_APPLICATION_CREDENTIALS environment variable to the path of your service account JSON key file and restart the application.");
                return;
            }
            Debug.WriteLine("[GridPage] AutoTranslate - configured from environment");
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
        var localeList = string.Join("\n", translationsNeeded.Select(kvp => $"  - {kvp.Key}: {kvp.Value} missing"));

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

                TranslationStatus = $"Translating to {baseLocale} ({translationsNeeded[baseLocale]} entries)...";
                Debug.WriteLine($"[GridPage] Translating {translationsNeeded[baseLocale]} entries to {baseLocale}");

                // Filter to entries missing GT for this locale
                var missingEntries = entriesToTranslate
                    .Where(e => !HasGTTranslationForBase(e, baseLocale))
                    .ToList();

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

    private bool CanAutoTranslate()
    {
        var canExecute = !IsTranslating && !IsSaving && !IsLoading && Entries.Count > 0;
        Debug.WriteLine($"[GridPage] CanAutoTranslate: {canExecute} (IsTranslating={IsTranslating}, IsSaving={IsSaving}, IsLoading={IsLoading}, Entries.Count={Entries.Count})");
        return canExecute;
    }

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
        Debug.WriteLine($"[GridPage] Saved {gtFileName}: added {addedCount}, removed {orphanedKeys.Count} orphaned");
    }

    /// <summary>
    /// Re-translate specific entries to all GT locales after enUS was changed
    /// </summary>
    private async Task RetranslateEntriesAsync(List<LocalizationEntryViewModel> entries)
    {
        if (entries.Count == 0) return;

        if (!_translateService.IsConfigured)
        {
            // Try to configure from environment variable
            if (!_translateService.TryConfigureFromEnvironment())
            {
                await _dialogService.ShowAlertAsync("Translation Skipped", 
                    "Google Translate service not configured. GT files were not updated.\n\n" +
                    "Please set the GOOGLE_APPLICATION_CREDENTIALS environment variable and use Auto-Translate to update GT files.");
                
                // Clear the changed flags since we're skipping
                foreach (var entry in entries)
                {
                    entry.ClearEnUSChangedFlag();
                }
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(_localizationDirectory))
        {
            await _dialogService.ShowAlertAsync("Translation Skipped", 
                "Localization directory not set. GT files were not updated.");
            return;
        }

        try
        {
            IsTranslating = true;
            TranslationProgress = 0;
            
            var baseLocales = LocaleDefinitions.GetGTBaseLocales().ToList();
            var processedLocales = 0;

            // Get unique enUS texts to translate
            var textsToTranslate = entries
                .Select(e => e.EnUS)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            Debug.WriteLine($"[GridPage] Re-translating {textsToTranslate.Count} unique texts to {baseLocales.Count} locales");

            foreach (var baseLocale in baseLocales)
            {
                var googleLangCode = LocaleDefinitions.GetGoogleLanguageCode(baseLocale);
                if (string.IsNullOrEmpty(googleLangCode))
                {
                    Debug.WriteLine($"[GridPage] No Google language code for {baseLocale}");
                    continue;
                }

                TranslationStatus = $"Re-translating to {baseLocale}...";
                Debug.WriteLine($"[GridPage] Translating to {baseLocale} ({googleLangCode})");

                var translations = await _translateService.TranslateBatchAsync(
                    textsToTranslate,
                    googleLangCode,
                    new Progress<TranslationProgress>(p =>
                    {
                        TranslationProgress = (double)p.ProcessedCount / p.TotalCount;
                        TranslationStatus = $"Translating to {baseLocale}: {p.ProcessedCount}/{p.TotalCount}";
                    }));

                // Update GT file - this will overwrite existing translations for these entries
                await UpdateGTTranslationsAsync(baseLocale, entries, translations);
                
                processedLocales++;
                TranslationProgress = (double)processedLocales / baseLocales.Count;
            }

            // Clear the changed flags
            foreach (var entry in entries)
            {
                entry.ClearEnUSChangedFlag();
            }

            TranslationStatus = "Re-translation complete!";
            await _dialogService.ShowAlertAsync("GT Files Updated", 
                $"Successfully re-translated {entries.Count} entry/entries to {baseLocales.Count} GT files.\n\n" +
                "Reloading data...");
            
            // Reload to show updated GT status
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GridPage] Re-translation error: {ex.Message}");
            Debug.WriteLine($"[GridPage] Stack trace: {ex.StackTrace}");
            
            await _dialogService.ShowAlertAsync("Re-Translation Error", 
                $"An error occurred during re-translation:\n\n{ex.Message}\n\n" +
                "Some GT files may not have been updated.");
        }
        finally
        {
            IsTranslating = false;
            TranslationProgress = 0;
            TranslationStatus = string.Empty;
        }
    }

    /// <summary>
    /// Update GT translations for specific entries (overwrite existing)
    /// </summary>
    private async Task UpdateGTTranslationsAsync(
        string baseLocale,
        List<LocalizationEntryViewModel> entries,
        Dictionary<string, string> translations)
    {
        if (string.IsNullOrWhiteSpace(_localizationDirectory)) return;

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

        // Remove orphaned entries
        var orphanedKeys = existingTranslations.Keys
            .Where(k => !validKeys.Contains(k))
            .ToList();
        
        foreach (var orphanedKey in orphanedKeys)
        {
            existingTranslations.Remove(orphanedKey);
        }

        // Update/add translations for the changed entries (OVERWRITE existing)
        var updatedCount = 0;
        foreach (var entry in entries)
        {
            if (translations.TryGetValue(entry.EnUS, out var translated))
            {
                existingTranslations[entry.GlueString] = translated;
                updatedCount++;
            }
        }

        Debug.WriteLine($"[GridPage] Updated {updatedCount} translations in {gtFileName}");

        // Write the GT file
        await WriteGTFileAsync(gtFilePath, baseLocale, existingTranslations);
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
            // Escape the value for Lua output (handles actual newlines, etc.)
            lines.Add($"    L[\"{key}\"] = \"{EscapeLuaString(value)}\"");
        }

        lines.Add("end");
        lines.Add("");

        await File.WriteAllLinesAsync(filePath, lines);
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

            // Save translations to GT file
            await SaveGTTranslationsAsync(baseLocale, missingEntries, translations);

            TranslationStatus = "Translation complete!";
            await _dialogService.ShowAlertAsync("Complete", 
                $"Translation to {baseLocale}-GT completed.\n\n" +
                $"Added {translations.Count} translations.\n\n" +
                "Reloading data to update GT status...");

            // Reload to reflect new GT status
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
