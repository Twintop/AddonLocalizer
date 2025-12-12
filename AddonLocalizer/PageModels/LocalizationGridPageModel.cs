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
    private bool _showOnlyMissingTranslations;

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
    private string _glueStringHeader = "Glue String ^";

    [ObservableProperty]
    private string _countHeader = "Count";

    [ObservableProperty]
    private string _concatHeader = "Concat";

    [ObservableProperty]
    private string _formatHeader = "Format";

    [ObservableProperty]
    private string _paramsHeader = "Params";

    [ObservableProperty]
    private string _filesHeader = "Files";

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private int _modifiedEntryCount;

    [ObservableProperty]
    private double _saveProgress;

    private ParseResult? _parseResult;
    private LocalizationDataSet? _localizationData;
    private string? _localizationDirectory;
    private string _currentSortColumn = "GlueString";
    private bool _sortAscending = true;

    public LocalizationGridPageModel(ILocalizationFileWriterService fileWriter, IDialogService dialogService)
    {
        _fileWriter = fileWriter;
        _dialogService = dialogService;
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

    partial void OnShowOnlyMissingTranslationsChanged(bool value)
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

        if (ShowOnlyMissingTranslations)
        {
            filtered = filtered.Where(e => e.IsMissingTranslations);
        }

        filtered = ApplySort(filtered);

        FilteredEntries = new ObservableCollection<LocalizationEntryViewModel>(filtered);
        FilteredCount = FilteredEntries.Count;
        HasData = FilteredCount > 0;
        Debug.WriteLine($"[GridPage] ApplyFilters: FilteredCount={FilteredCount}, HasData={HasData}");
    }

    private IEnumerable<LocalizationEntryViewModel> ApplySort(IEnumerable<LocalizationEntryViewModel> items)
    {
        var ordered = _currentSortColumn switch
        {
            "GlueString" => _sortAscending 
                ? items.OrderBy(e => e.GlueString) 
                : items.OrderByDescending(e => e.GlueString),
            "Count" => _sortAscending 
                ? items.OrderBy(e => e.OccurrenceCount) 
                : items.OrderByDescending(e => e.OccurrenceCount),
            "Concat" => _sortAscending 
                ? items.OrderBy(e => e.HasConcatenation) 
                : items.OrderByDescending(e => e.HasConcatenation),
            "Format" => _sortAscending 
                ? items.OrderBy(e => e.UsedInStringFormat) 
                : items.OrderByDescending(e => e.UsedInStringFormat),
            "Params" => _sortAscending 
                ? items.OrderBy(e => e.ParameterCount) 
                : items.OrderByDescending(e => e.ParameterCount),
            "Files" => _sortAscending 
                ? items.OrderBy(e => e.FileLocations) 
                : items.OrderByDescending(e => e.FileLocations),
            _ => items.OrderBy(e => e.GlueString)
        };

        return ordered;
    }

    private void UpdateColumnHeaders()
    {
        var sortIndicator = _sortAscending ? "^" : "v";
        
        GlueStringHeader = _currentSortColumn == "GlueString" ? $"Glue String {sortIndicator}" : "Glue String";
        CountHeader = _currentSortColumn == "Count" ? $"Count {sortIndicator}" : "Count";
        ConcatHeader = _currentSortColumn == "Concat" ? $"Concat {sortIndicator}" : "Concat";
        FormatHeader = _currentSortColumn == "Format" ? $"Format {sortIndicator}" : "Format";
        ParamsHeader = _currentSortColumn == "Params" ? $"Params {sortIndicator}" : "Params";
        FilesHeader = _currentSortColumn == "Files" ? $"Files {sortIndicator}" : "Files";
    }

    [RelayCommand]
    private void SortByColumn(string columnName)
    {
        if (_currentSortColumn == columnName)
        {
            // Toggle sort direction
            _sortAscending = !_sortAscending;
        }
        else
        {
            // New column, sort ascending
            _currentSortColumn = columnName;
            _sortAscending = true;
        }

        UpdateColumnHeaders();
        ApplyFilters();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        ShowOnlyConcatenated = false;
        ShowOnlyStringFormat = false;
        ShowOnlyWithParameters = false;
        ShowOnlyMissingTranslations = false;
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
}
