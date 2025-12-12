using System.Collections.ObjectModel;
using AddonLocalizer.Core.Models;
using AddonLocalizer.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace AddonLocalizer.PageModels;

public partial class LocalizationGridPageModel : ObservableObject, IQueryAttributable
{
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

    private ParseResult? _parseResult;
    private string _currentSortColumn = "GlueString";
    private bool _sortAscending = true;

    public LocalizationGridPageModel()
    {
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
                                return new LocalizationEntryViewModel(kvp.Key, kvp.Value);
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
                    StatusMessage = HasData ? $"Showing {FilteredCount} of {TotalCount} entries" : "No entries to display";
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

    [RelayCommand]
    private async Task ExportData()
    {
        // TODO: Implement export functionality
        await Task.CompletedTask;
    }
}
