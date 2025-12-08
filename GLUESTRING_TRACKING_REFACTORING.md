# Glue String Tracking Refactoring

## Changes Made

The parser has been refactored to track glue strings differently based on whether they have concatenation or not.

## New Behavior

### Non-Concatenated Strings
For glue strings that **do NOT** have concatenation:
- Store the glue string value
- Track total occurrence count
- **Do NOT store** file paths or line numbers

```csharp
// Example: L["SimpleMessage"] appears 5 times
{
    GlueString = "SimpleMessage",
    HasConcatenation = false,
    OccurrenceCount = 5,
    Locations = [] // Empty - not tracked for non-concatenated
}
```

### Concatenated Strings
For glue strings that **DO** have concatenation (line contains `..`):
- Store the glue string value
- Track total occurrence count
- **Store every location** (file path + line number)

```csharp
// Example: L["ConcatMessage"] .. "text" appears 3 times
{
    GlueString = "ConcatMessage",
    HasConcatenation = true,
    OccurrenceCount = 3,
    Locations = [
        { FilePath = "addon.lua", LineNumber = 15 },
        { FilePath = "addon.lua", LineNumber = 42 },
        { FilePath = "utils.lua", LineNumber = 8 }
    ]
}
```

## New Models

### GlueStringLocation
```csharp
public class GlueStringLocation
{
    public string FilePath { get; set; }
    public int LineNumber { get; set; }
}
```

### GlueStringInfo
```csharp
public class GlueStringInfo
{
    public string GlueString { get; set; }
    public bool HasConcatenation { get; set; }
    public int OccurrenceCount { get; set; }
    public List<GlueStringLocation> Locations { get; set; }
}
```

### ParseResult
```csharp
public class ParseResult
{
    public Dictionary<string, GlueStringInfo> GlueStrings { get; set; }
    
    // Convenience properties
    public IEnumerable<GlueStringInfo> NonConcatenated => 
        GlueStrings.Values.Where(g => !g.HasConcatenation);
    
    public IEnumerable<GlueStringInfo> Concatenated => 
        GlueStrings.Values.Where(g => g.HasConcatenation);
}
```

## Usage Examples

### Get All Glue Strings
```csharp
var result = await parser.ParseDirectoryAsync(@"C:\WoW\AddOns\MyAddon");

foreach (var (key, info) in result.GlueStrings)
{
    Console.WriteLine($"{info.GlueString}: {info.OccurrenceCount} occurrences");
}
```

### Get Only Non-Concatenated Strings
```csharp
var result = await parser.ParseDirectoryAsync(addonPath);

foreach (var info in result.NonConcatenated)
{
    Console.WriteLine($"{info.GlueString} appears {info.OccurrenceCount} times");
    // info.Locations will be empty
}
```

### Get Concatenated Strings with Locations
```csharp
var result = await parser.ParseDirectoryAsync(addonPath);

foreach (var info in result.Concatenated)
{
    Console.WriteLine($"{info.GlueString} (concatenated):");
    foreach (var location in info.Locations)
    {
        Console.WriteLine($"  {location.FilePath}:{location.LineNumber}");
    }
}
```

### Check If String Has Concatenation
```csharp
var result = await parser.ParseFileAsync("addon.lua");

if (result.GlueStrings.TryGetValue("MyString", out var info))
{
    if (info.HasConcatenation)
    {
        Console.WriteLine($"Found at {info.Locations.Count} concatenated locations");
    }
    else
    {
        Console.WriteLine($"Found {info.OccurrenceCount} times (not concatenated)");
    }
}
```

## Removed Classes

The following classes were removed as they're no longer needed:
- `LocalizationEntry` (replaced by `GlueStringInfo` + `GlueStringLocation`)

## Benefits

### ? Optimized Storage
- Non-concatenated strings don't waste memory storing locations
- Only track what's needed for each case

### ? Clear Intent
- `HasConcatenation` flag makes it explicit
- Empty `Locations` list for non-concatenated strings
- Full location tracking for concatenated strings

### ? Efficient Queries
- Convenience properties `NonConcatenated` and `Concatenated`
- Dictionary lookup by glue string
- No filtering needed to get counts

### ? Memory Efficient
- For addons with thousands of non-concatenated strings, this saves significant memory
- Only concatenated strings (typically much fewer) store location data

## Test Results

? **All 28 tests passing** (95ms execution time)

## Files Modified

1. **AddonLocalizer.Core\Models\LocalizationEntry.cs** (RENAMED/REFACTORED)
   - Renamed to models that match new behavior
   - `GlueStringLocation` - stores file + line number
   - `GlueStringInfo` - stores string info + conditional location tracking
   - `ParseResult` - dictionary-based result with convenience properties

2. **AddonLocalizer.Core\Services\LuaLocalizationParserService.cs** (MODIFIED)
   - Updated to use new model structure
   - Added `ProcessLines` helper method
   - Added `MergeResults` for directory parsing
   - Conditional location tracking based on concatenation

3. **AddonLocalizer.Tests\Core\Services\LuaLocalizationParserServiceTests.cs** (MODIFIED)
   - All 28 tests updated to work with new structure
   - Added tests for location tracking behavior
   - Added tests for `NonConcatenated` and `Concatenated` properties

## Migration Notes

### Breaking Changes
- `ParseResult.AllGlueStrings` ? `ParseResult.GlueStrings` (now a `Dictionary<string, GlueStringInfo>`)
- `ParseResult.AllEntries` ? Removed
- `ParseResult.ConcatenatedEntries` ? Now `ParseResult.Concatenated` (convenience property)

### Migration Example

**Before:**
```csharp
var result = await parser.ParseFileAsync("addon.lua");
var strings = result.AllGlueStrings; // HashSet<string>
var concatenated = result.ConcatenatedEntries; // List<LocalizationEntry>
```

**After:**
```csharp
var result = await parser.ParseFileAsync("addon.lua");
var strings = result.GlueStrings.Keys; // Dictionary keys
var concatenated = result.Concatenated; // IEnumerable<GlueStringInfo>
```
