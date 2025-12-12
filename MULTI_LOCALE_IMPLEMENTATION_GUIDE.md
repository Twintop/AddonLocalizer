# Multi-Locale Implementation - Remaining Manual Updates

## Files Successfully Created/Updated:

? AddonLocalizer.Core\Models\LocalizationEntry.cs - Added locale models
? AddonLocalizer.Core\Services\FileSystemService.cs - Added UTF-8 + write methods  
? AddonLocalizer.Core\Interfaces\IFileSystemService.cs - Added write method signatures
? AddonLocalizer.Core\Services\LuaLocalizationParserService.cs - Added locale parsing methods
? AddonLocalizer.Core\Interfaces\ILuaLocalizationParserService.cs - Updated interface
? AddonLocalizer.Core\Services\LocalizationFileWriterService.cs - CREATED
? AddonLocalizer.Core\Interfaces\ILocalizationFileWriterService.cs - CREATED
? AddonLocalizer.Core\ServiceCollectionExtensions.cs - Registered file writer
? AddonLocalizer\Services\IDialogService.cs - CREATED
? AddonLocalizer\Converters\CommonConverters.cs - Added new converters
? AddonLocalizer\App.xaml - Registered converters
? AddonLocalizer\MauiProgram.cs - Registered DialogService

## Files Requiring Manual Updates:

### 1. AddonLocalizer\Models\LocalizationEntryViewModel.cs

**Current file is open in your editor. Replace the entire content with the enhanced version that includes:**

- 15 locale properties (EnUS, EnGB, EnTW, EnCN, DeDE, EsES, EsMX, FrFR, ItIT, KoKR, PtBR, PtPT, RuRU, ZhCN, ZhTW)
- Change tracking dictionary `_originalTranslations`
- Methods: `LoadTranslations()`, `GetAndStoreTranslation()`, `HasChanges()`, `GetCurrentTranslations()`, `GetChangedTranslations()`, `ResetChanges()`, `CommitChanges()`
- Properties: `TranslationCoverage`, `IsMissingTranslations`, `TranslatedLocaleCount`

**Search for:** `public partial class LocalizationEntryViewModel`

**Replace entire class with the version from my previous response in Phase 3.**

---

### 2. AddonLocalizer\PageModels\LocalizationGridPageModel.cs

**Current file is open in your editor. This needs major updates:**

**Add constructor parameter:**
```csharp
private readonly IDialogService _dialogService;

public LocalizationGridPageModel(ILocalizationFileWriterService fileWriter, IDialogService dialogService)
{
    _fileWriter = fileWriter;
    _dialogService = dialogService;
    Debug.WriteLine("[GridPage] Constructor called");
}
```

**Add new properties:**
```csharp
[ObservableProperty]
private bool _isSaving = false;

[ObservableProperty]
private bool _hasUnsavedChanges;

[ObservableProperty]
private int _modifiedEntryCount;

[ObservableProperty]
private double _saveProgress;

private string? _localizationDirectory;
```

**Update ApplyQueryAttributes to capture LocalizationDirectory:**
```csharp
if (query.TryGetValue("LocalizationDirectory", out var locDir) && locDir is string localizationDir)
{
    _localizationDirectory = localizationDir;
    Debug.WriteLine($"[GridPage] Received LocalizationDirectory: {localizationDir}");
}
```

**Add in LoadDataAsync after creating entries:**
```csharp
// Setup property change monitoring for entries
foreach (var entry in Entries)
{
    entry.PropertyChanged += Entry_PropertyChanged;
}

UpdateChangeTracking();
```

**Add new methods:**
```csharp
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
    
    Debug.WriteLine($"[GridPage] Change tracking updated: {ModifiedEntryCount} modified entries");
}
```

**Replace the existing SaveChangesCommand, DiscardChangesCommand with the enhanced versions from Phase 5.**

**Add new commands:**
```csharp
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
```

---

### 3. AddonLocalizer\PageModels\LocalizationHomePageModel.cs

**Add new properties:**
```csharp
[ObservableProperty]
private int _loadedLocales;

[ObservableProperty]
private bool _hasLocalizationFolder;
```

**Add methods:**
```csharp
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
```

**Update ParseDirectory method to:**
- Parse addon directory excluding Localization folder
- Load locale files from Localization directory
- Pass LocalizationDirectory in navigation parameters

**See Phase 4/5 responses for complete implementation.**

---

### 4. AddonLocalizer\Pages\LocalizationGridPage.xaml

**Replace the entire XAML with the version from Phase 5 that includes:**
- 15 locale columns in DataGrid
- Save/Discard bar with progress indicator
- Updated filter section
- Status bar at bottom

---

### 5. AddonLocalizer\Pages\LocalizationHomePage.xaml

**Update to add:**
- Localization folder indicator
- Loaded locales count in statistics
- Progress bar during parsing

---

## Quick Verification Checklist:

After making all changes, verify:

1. ? Solution builds without errors
2. ? All 15 locale columns appear in the grid
3. ? Editing a cell marks it as changed
4. ? Save button appears when changes exist
5. ? Save confirmation dialog shows correct counts
6. ? Files are saved with UTF-8 encoding
7. ? Backup files are created

## Testing Steps:

1. Select a WoW addon directory with a Localization folder
2. Click "Parse Directory"
3. Verify locale files load (check status message)
4. Edit a translation in any locale column
5. Verify orange save bar appears
6. Click "Save Changes"
7. Verify confirmation dialog
8. Verify backup is created in Localization folder
9. Verify changes persist after reload

---

**Note:** The complete code for each file is available in my previous Phase 3, 4, and 5 responses. 
Copy those implementations directly into your files.
