using AddonLocalizer.Core.Interfaces;
using AddonLocalizer.Core.Models;
using AddonLocalizer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AddonLocalizer.PageModels;

public partial class LocalizationHomePageModel : ObservableObject
{
    private readonly IFolderPickerService _folderPickerService;
    private readonly ILuaLocalizationParserService _parserService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string? _selectedDirectory;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Select a directory to begin";

    [ObservableProperty]
    private int _totalEntries;

    [ObservableProperty]
    private int _concatenatedEntries;

    [ObservableProperty]
    private int _stringFormatEntries;

    [ObservableProperty]
    private int _loadedLocales;

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _canCancel;

    [ObservableProperty]
    private bool _hasLocalizationFolder;

    public LocalizationHomePageModel(
        IFolderPickerService folderPickerService,
        ILuaLocalizationParserService parserService)
    {
        _folderPickerService = folderPickerService;
        _parserService = parserService;

        LoadRecentDirectory();
    }

    private void LoadRecentDirectory()
    {
        if (Preferences.Default.ContainsKey("last_directory"))
        {
            SelectedDirectory = Preferences.Default.Get("last_directory", string.Empty);
            CheckForLocalizationFolder();
        }
    }

    partial void OnSelectedDirectoryChanged(string? value)
    {
        CheckForLocalizationFolder();
    }

    private void CheckForLocalizationFolder()
    {
        if (string.IsNullOrWhiteSpace(SelectedDirectory))
        {
            HasLocalizationFolder = false;
            return;
        }

        var localizationDir = Path.Combine(SelectedDirectory, "Localization");
        HasLocalizationFolder = Directory.Exists(localizationDir);
    }

    [RelayCommand]
    private async Task SelectDirectory()
    {
        var directory = await _folderPickerService.PickFolderAsync();
        
        if (!string.IsNullOrWhiteSpace(directory))
        {
            SelectedDirectory = directory;
            Preferences.Default.Set("last_directory", directory);
            StatusMessage = $"Directory selected: {directory}";
        }
    }

    [RelayCommand]
    private async Task ParseDirectory()
    {
        if (string.IsNullOrWhiteSpace(SelectedDirectory))
        {
            StatusMessage = "Please select a directory first";
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsLoading = true;
            CanCancel = true;
            Progress = 0;
            StatusMessage = "Parsing addon files...";

            // Step 1: Parse the addon directory for glue string usage
            Progress = 0.1;
            var parseResult = await Task.Run(() =>
            {
                try
                {
                    // Exclude Localization directory when scanning for usage
                    return _parserService.ParseDirectory(SelectedDirectory, ["Localization"]);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }, _cancellationTokenSource.Token);

            if (parseResult == null)
            {
                StatusMessage = "Parsing cancelled";
                return;
            }

            Progress = 0.4;
            StatusMessage = "Loading localization files...";

            // Step 2: Check if Localization directory exists and load locale files
            LocalizationDataSet? localizationData = null;
            var localizationDir = Path.Combine(SelectedDirectory, "Localization");
            
            // Step 2.5: Parse Localization.lua and LocalizationPost.lua to exclude their definitions
            var excludedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            if (Directory.Exists(localizationDir))
            {
                // Parse Localization.lua for definitions
                var localizationPath = Path.Combine(localizationDir, "Localization.lua");
                if (File.Exists(localizationPath))
                {
                    try
                    {
                        var locKeys = await _parserService.ParseLocalizationDefinitionsAsync(localizationPath);
                        excludedKeys.UnionWith(locKeys);
                        StatusMessage = $"Found {locKeys.Count} entries in Localization.lua";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Warning: Error parsing Localization.lua: {ex.Message}";
                    }
                }

                // Parse LocalizationPost.lua for definitions and usages
                var localizationPostPath = Path.Combine(localizationDir, "LocalizationPost.lua");
                if (File.Exists(localizationPostPath))
                {
                    try
                    {
                        // Get definitions (left side)
                        var postKeys = await _parserService.ParseLocalizationDefinitionsAsync(localizationPostPath);
                        excludedKeys.UnionWith(postKeys);
                        
                        // Get usages (right side) - these are references to other localization strings
                        var usedKeys = await _parserService.ParseLocalizationUsagesAsync(localizationPostPath);
                        
                        // Add these to code usage since they're being used
                        foreach (var key in usedKeys)
                        {
                            if (!parseResult.GlueStrings.ContainsKey(key))
                            {
                                parseResult.GlueStrings[key] = new GlueStringInfo
                                {
                                    GlueString = key,
                                    HasConcatenation = false,
                                    OccurrenceCount = 1
                                };
                            }
                        }
                        
                        StatusMessage = $"Found {postKeys.Count} definitions and {usedKeys.Count} usages in LocalizationPost.lua";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Warning: Error parsing LocalizationPost.lua: {ex.Message}";
                    }
                }

                // Remove excluded keys from parse result
                foreach (var key in excludedKeys.ToList())
                {
                    parseResult.GlueStrings.Remove(key);
                }
            }

            Progress = 0.6;
            StatusMessage = "Loading locale translations...";
            
            if (Directory.Exists(localizationDir))
            {
                try
                {
                    localizationData = await Task.Run(() =>
                    {
                        try
                        {
                            // Parse all locale files, excluding *GT.lua (Google Translate)
                            return _parserService.ParseLocalizationDirectoryAsync(localizationDir, ["GT.lua"]).Result;
                        }
                        catch (OperationCanceledException)
                        {
                            return null;
                        }
                    }, _cancellationTokenSource.Token);

                    if (localizationData != null)
                    {
                        LoadedLocales = localizationData.LoadedLocales.Count();

                        // Load GT (Google Translate) files
                        StatusMessage = "Loading Google Translate files...";
                        await _parserService.LoadGTFilesAsync(localizationDir, localizationData);
                        
                        var gtLocaleCount = localizationData.LoadedGTLocales.Count();
                        if (gtLocaleCount > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Home] Loaded {gtLocaleCount} GT locales");
                        }
                        
                        // Parse format parameters from enUS if available
                        var enUsFile = Path.Combine(localizationDir, "enUS.lua");
                        if (File.Exists(enUsFile))
                        {
                            var formatParams = await _parserService.ParseFormatParametersAsync(enUsFile);
                            
                            // Add format parameters to matching glue strings
                            foreach (var (key, parameters) in formatParams)
                            {
                                if (parseResult.GlueStrings.TryGetValue(key, out var info))
                                {
                                    info.FormatParameters = parameters;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Warning: Error loading localization files: {ex.Message}";
                    // Continue with just the parse result
                }
            }

            Progress = 1.0;

            TotalEntries = parseResult.GlueStrings.Count;
            ConcatenatedEntries = parseResult.Concatenated.Count();
            StringFormatEntries = parseResult.WithStringFormat.Count();
            HasData = TotalEntries > 0;

            var localeInfo = LoadedLocales > 0 ? $" with {LoadedLocales} locales" : "";
            var excludedInfo = excludedKeys.Count > 0 ? $" ({excludedKeys.Count} excluded from Localization files)" : "";
            StatusMessage = $"Parsed {TotalEntries} localization entries{localeInfo}{excludedInfo}";

            // Navigate to grid page with both parse result and localization data
            var navigationParams = new Dictionary<string, object>
            {
                { "ParseResult", parseResult }
            };

            if (localizationData != null)
            {
                navigationParams.Add("LocalizationData", localizationData);
            }

            // Pass the localization directory for saving
            if (Directory.Exists(localizationDir))
            {
                navigationParams.Add("LocalizationDirectory", localizationDir);
            }

            await Shell.Current.GoToAsync("localizations", navigationParams);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Parsing cancelled by user";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            CanCancel = false;
            Progress = 0;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void CancelParsing()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "Cancelling...";
    }

    [RelayCommand]
    private void ClearDirectory()
    {
        SelectedDirectory = null;
        Preferences.Default.Remove("last_directory");
        StatusMessage = "Select a directory to begin";
        HasData = false;
        TotalEntries = 0;
        ConcatenatedEntries = 0;
        StringFormatEntries = 0;
        LoadedLocales = 0;
        HasLocalizationFolder = false;
    }
}
