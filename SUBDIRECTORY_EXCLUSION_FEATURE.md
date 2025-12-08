# Subdirectory Exclusion Feature

## Overview

Added the ability to exclude specific subdirectories when parsing a directory for localization strings. This is particularly useful for excluding the `Localization` subdirectory which contains the translated values themselves.

## Changes Made

### Interface Update
**File:** `AddonLocalizer.Core\Interfaces\ILuaLocalizationParserService.cs`

Added optional `excludeSubdirectories` parameter to directory parsing methods:

```csharp
Task<ParseResult> ParseDirectoryAsync(string directoryPath, string[]? excludeSubdirectories = null);
ParseResult ParseDirectory(string directoryPath, string[]? excludeSubdirectories = null);
```

### Service Implementation
**File:** `AddonLocalizer.Core\Services\LuaLocalizationParserService.cs`

Added:
- `FilterExcludedPaths()` - Filters file paths to exclude specified subdirectories
- Path segment matching algorithm for reliable subdirectory exclusion
- Works with both relative and absolute paths

### Test Coverage
**File:** `AddonLocalizer.Tests\Core\Services\LuaLocalizationParserServiceTests.cs`

Added 5 new tests:
- Single subdirectory exclusion
- Multiple subdirectory exclusion
- Null exclusion list (parse all)
- Empty exclusion list (parse all)
- Synchronous version with exclusion

## Usage

### Exclude Single Subdirectory
```csharp
var parser = new LuaLocalizationParserService();
var result = await parser.ParseDirectoryAsync(
    @"C:\WoW\AddOns\MyAddon",
    new[] { "Localization" }
);
```

### Exclude Multiple Subdirectories
```csharp
var result = await parser.ParseDirectoryAsync(
    addonPath,
    new[] { "Localization", "Tests", "Docs" }
);
```

### No Exclusions (Default Behavior)
```csharp
// These are all equivalent
var result = await parser.ParseDirectoryAsync(addonPath);
var result = await parser.ParseDirectoryAsync(addonPath, null);
var result = await parser.ParseDirectoryAsync(addonPath, Array.Empty<string>());
```

## How It Works

The exclusion works by matching path segments rather than simple string matching:

1. File path: `C:\Addon\Localization\enUS.lua`
2. Excluded: `["Localization"]`
3. Splits paths into segments and checks for consecutive matches
4. Case-insensitive matching
5. Handles both forward and back slashes

### Path Matching Examples

? **Excluded:**
- `Localization/enUS.lua` (matches `Localization`)
- `Functions/Localization/helper.lua` (matches `Localization`)
- `subfolder/Localization/test.lua` (matches `Localization`)

? **Not Excluded:**
- `LocalizationUtils.lua` (not a directory segment)
- `Functions/MyLocalizationFile.lua` (not a segment match)

## Real-World Example

### TwintopInsanityBar Addon Results

**With Localization Excluded:**
```
Total unique glue strings found: 1773
- Non-Concatenated: 1393 strings
- Concatenated: 380 strings

Examples:
  L["AbilityThresholdLinesHeader"] ? 19 occurrences
  L["CheckboxEnabledQuestion"] ? 37 occurrences (concatenated)
```

**Why Exclude Localization?**
The `Localization` directory contains files like:
- `enUS.lua` - English translations
- `deDE.lua` - German translations  
- `frFR.lua` - French translations

These files define the **values** for localization strings, not their usage:
```lua
-- Localization/enUS.lua
L["WelcomeMessage"] = "Welcome to the addon!"
L["GoodbyeMessage"] = "Thanks for using our addon"
```

We want to find where these strings are **used** in the code, not where they're defined.

## Test Results

? **All 33 tests passing** (133ms execution time)

New tests verify:
- Single exclusion works correctly
- Multiple exclusions work correctly
- Null/empty exclusions don't break existing behavior
- Both async and sync versions work

## Benefits

### ? Accurate Results
- Excludes translation files from analysis
- Only tracks actual usage in code

### ? Performance
- Fewer files to parse
- Faster results

### ? Flexibility
- Exclude any subdirectories you want
- Works with nested subdirectories
- Case-insensitive matching

### ? Backward Compatible
- Optional parameter
- Default behavior unchanged
- Existing code continues to work

## Files Modified

1. **AddonLocalizer.Core\Interfaces\ILuaLocalizationParserService.cs**
   - Added `excludeSubdirectories` parameter to 2 methods

2. **AddonLocalizer.Core\Services\LuaLocalizationParserService.cs**
   - Added `FilterExcludedPaths()` method
   - Updated `ParseDirectoryAsync()` and `ParseDirectory()`

3. **AddonLocalizer.Tests\Core\Services\LuaLocalizationParserServiceTests.cs**
   - Added 5 comprehensive tests

4. **ParserTest\Program.cs**
   - Updated demo to show exclusion feature

## Summary

The subdirectory exclusion feature allows you to exclude translation files and other non-code directories when scanning for localization string usage. This results in more accurate tracking of where strings are actually used in your addon code.
