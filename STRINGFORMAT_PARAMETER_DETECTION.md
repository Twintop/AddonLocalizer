# String.Format() Parameter Detection

## Overview

Many localization strings in WoW addons use Lua's `string.format()` function to insert dynamic values into text. These strings require special attention when creating translations because they contain format specifiers like `%s`, `%d`, `%1$s`, etc.

## The Problem

### Example from enUS.lua
```lua
L["IsBarEnabledForSpecTooltip"] = "Locks/unlocks the %s Resource Bar."
```

### Usage in Code
```lua
GameTooltip:AddLine(string.format(L["IsBarEnabledForSpecTooltip"], specName))
```

**Challenge:** When creating new localization entries or translating to other languages, you need to know which strings require format parameters and maintain the same number/type of parameters.

## Solution

The parser now detects when `L["..."]` is used as the **first parameter** in a `string.format()` call and:
1. Sets the `UsedInStringFormat` flag on the glue string
2. Tracks all locations where it's used with `string.format()`
3. Reports these strings separately for translator awareness

## Detection Logic

### Regex Pattern
```csharp
@"string\.format\s*\(\s*L\[""([^""]+)""\]"
```

This matches:
- `string.format(L["Key"]`
- `string.format( L["Key"]` (with whitespace)
- `string.format(  L["Key"]  ` (multiple spaces)

### Why First Parameter Only?

**Correct (Template):**
```lua
string.format(L["Template"], value1, value2)
-- L["Template"] is the format string with %s/%d placeholders
```

**Incorrect (Parameter):**
```lua
string.format("Static Template", L["NotTemplate"])
-- L["NotTemplate"] is a value being inserted, not the template
```

Only the first parameter is the **format template** that needs special translation attention.

## Model Updates

### GlueStringInfo
```csharp
public class GlueStringInfo
{
    public string GlueString { get; set; }
    public bool HasConcatenation { get; set; }
    public bool UsedInStringFormat { get; set; }  // NEW
    public int OccurrenceCount { get; set; }
    public List<GlueStringLocation> Locations { get; set; }
    public List<GlueStringLocation> StringFormatLocations { get; set; }  // NEW
}
```

### ParseResult
```csharp
public class ParseResult
{
    // ... existing properties ...
    
    public IEnumerable<GlueStringInfo> WithStringFormat =>
        GlueStrings.Values.Where(g => g.UsedInStringFormat);
}
```

## Real-World Results: TwintopInsanityBar

### Summary Statistics
```
Total strings used in code: 1,781
With string.format() parameters: 47 (2.6%)
```

### Examples Found

#### High-Usage Template
```
L["IsBarEnabledForSpecTooltip"] - Used in string.format() 40x
  - DeathKnightOptions.lua:1496
  - DeathKnightOptions.lua:1928
  - ... (38 more locations)
```

#### Error Message Templates
```
L["LSMInvalidBarBackgroundTexture"] - Used in string.format() 1x
L["LSMInvalidBarBorderTexture"] - Used in string.format() 1x
L["LSMInvalidFont"] - Used in string.format() 1x
L["LSMInvalidSound"] - Used in string.format() 2x
```

#### UI Element Templates
```
L["BarAndSecondardTexturesHeader"] - Used in string.format() 1x
L["BarTextDeleteConfirmation"] - Used in string.format() 1x
L["FlashAlpha"] - Used in string.format() 1x
```

## Output Files

### 1. missing_enUS_entries.lua
Now includes parameter indicators:
```lua
L["SomeKey"] = "SomeKey"
L["FormatKey"] = "FormatKey" -- REQUIRES FORMAT PARAMETERS
```

### 2. stringformat_entries.txt
Complete list of all strings that use `string.format()`:
```
IsBarEnabledForSpecTooltip
LSMInvalidBarBackgroundTexture
LSMInvalidFont
...
```

## Usage in Analysis

### Console Output
```
?? ANALYSIS SUMMARY
Total strings used in code: 1,781
With string.format() parameters: 47
...

?? STRING.FORMAT USAGE (47 entries)
These strings require format parameters (e.g., %s, %d):
  L["IsBarEnabledForSpecTooltip"] - Used in string.format() 40x
    - DeathKnightOptions.lua:1496
    - DeathKnightOptions.lua:1928
  ...
```

### Missing Entries
```
? MISSING IN enUS.lua (166 entries)
  L["SimpleKey"] = "SimpleKey"  -- Used 5x
  L["FormatKey"] = "FormatKey" [NEEDS PARAMS]  -- Used 3x
```

## Translation Guidelines

### Strings WITH string.format()
```lua
-- enUS.lua
L["IsBarEnabledForSpecTooltip"] = "Locks/unlocks the %s Resource Bar."

-- deDE.lua (German)
L["IsBarEnabledForSpecTooltip"] = "Sperrt/entsperrt die %s-Ressourcenleiste."
-- MUST maintain %s placeholder!

-- frFR.lua (French)  
L["IsBarEnabledForSpecTooltip"] = "Verrouille/déverrouille la barre de ressources %s."
-- %s can be moved but must exist!
```

### Strings WITHOUT string.format()
```lua
-- enUS.lua
L["SimpleMessage"] = "This is a simple message."

-- deDE.lua (German)
L["SimpleMessage"] = "Dies ist eine einfache Nachricht."
-- No format specifiers to worry about
```

## Common Format Specifiers

### String Replacement
```lua
"%s"       -- Single string
"%1$s"     -- First parameter (positional)
"%2$s"     -- Second parameter (positional)
```

### Number Replacement
```lua
"%d"       -- Integer
"%f"       -- Float
"%.2f"     -- Float with 2 decimals
```

### Mixed Example
```lua
L["ComplexFormat"] = "Player %s has %d items (%.1f%% full)"
-- Requires: string, integer, float
```

## Test Coverage

Added 6 comprehensive tests (27 total tests passing):

### 1. ParseFileAsync_DetectsStringFormatUsage
```csharp
string.format(L["FormatString"], value)
// Marks FormatString with UsedInStringFormat = true
```

### 2. ParseFileAsync_StringFormatWithMultipleParameters
```csharp
string.format(L["Template"], arg1, arg2, arg3)
// Tracks multiple usages of same template
```

### 3. ParseFileAsync_StringFormatNotFirstParameter
```csharp
string.format("literal", L["NotFirst"])
// Does NOT mark NotFirst (it's not the template)
```

### 4. ParseResult_WithStringFormat_ReturnsCorrectly
Tests the `WithStringFormat` helper property

### 5. ParseFileAsync_StringFormatWithWhitespace
Handles various whitespace patterns

### 6. Synchronous version test

## Benefits

### ? Translator Awareness
Translators know which strings need special attention for format specifiers

### ? Quality Assurance
Prevents translation errors where format specifiers are removed or mismatched

### ? Documentation
Automatically documents which strings are parameterized

### ? Prioritization
High-usage templates (like `IsBarEnabledForSpecTooltip` used 40x) can be prioritized

## Integration with Workflow

### Step 1: Generate Missing Entries
```bash
dotnet run --project ParserTest\ParserTest.csproj
```

### Step 2: Review Output
```
missing_enUS_entries.lua - Entries marked with "REQUIRES FORMAT PARAMETERS"
stringformat_entries.txt - Complete list of parameterized strings
```

### Step 3: Create Translations
For entries marked `[NEEDS PARAMS]`:
1. Examine the English version for format specifiers
2. Ensure translation maintains all format specifiers
3. Test with actual parameter values

### Example Workflow
```lua
// Generated
L["IsBarEnabledForSpecTooltip"] = "IsBarEnabledForSpecTooltip" -- REQUIRES FORMAT PARAMETERS

// Find in enUS.lua
L["IsBarEnabledForSpecTooltip"] = "Locks/unlocks the %s Resource Bar."

// Create proper English
L["IsBarEnabledForSpecTooltip"] = "Locks/unlocks the %s Resource Bar."
// Note: Has one %s that will be replaced with spec name

// Translate to German (example)
L["IsBarEnabledForSpecTooltip"] = "Sperrt/entsperrt die %s-Ressourcenleiste."
// Maintained the %s placeholder
```

## Statistics Summary

**TwintopInsanityBar Analysis:**
- Total glue strings: 1,781
- Using string.format(): 47 (2.6%)
- Most used: `IsBarEnabledForSpecTooltip` (40 locations)
- Average usage: ~1-2 times per string
- Categories:
  - UI tooltips: ~15 strings
  - Error messages: ~8 strings
  - LSM validation: ~10 strings
  - Class-specific: ~14 strings

## Summary

The string.format() detection feature:
1. **Identifies** parameterized localization strings
2. **Tracks** all usage locations
3. **Flags** entries that need format specifiers
4. **Documents** which strings require special translation care
5. **Prevents** translation errors from missing or malformed parameters

This ensures that dynamic content insertion works correctly across all language translations!
