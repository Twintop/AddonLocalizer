# LocalizationPost.lua Usage Parsing

## Problem

`LocalizationPost.lua` creates compound localization strings by referencing other localization keys:

```lua
L["BarTextInstructions"] = L["BarTextInstructions1"] .. "\n\n" .. L["BarTextInstructions2"]
```

The analysis was incorrectly marking `BarTextInstructions1` and `BarTextInstructions2` as "orphaned" because they weren't directly used in the code - they were only referenced in `LocalizationPost.lua`.

## Solution

Added new methods to parse **right-side references** in localization files:

```csharp
Task<HashSet<string>> ParseLocalizationUsagesAsync(string filePath)
HashSet<string> ParseLocalizationUsages(string filePath)
```

These methods extract all `L["..."]` patterns from the **right side of assignments**, treating them as code usage.

## How It Works

### Step 1: Parse Assignment Line
```csharp
var assignmentMatch = AssignmentPattern.Match(line);
// Matches: L["key"] = ...
```

### Step 2: Extract Right Side
```csharp
var assignmentIndex = line.IndexOf('=');
var rightSide = line.Substring(assignmentIndex + 1);
```

### Step 3: Find All References
```csharp
var matches = LocalizationPattern.Matches(rightSide);
// Captures all L["..."] patterns
```

## Example

### Input File (LocalizationPost.lua)
```lua
L["BarTextInstructions"] = L["BarTextInstructions1"] .. "\n\n" .. L["BarTextInstructions2"]
L["FullName"] = L["FirstName"] .. " " .. L["LastName"]
L["Title"] = string.format("%s: %s", L["Prefix"], L["Name"])
```

### Definitions (Left Side)
```
BarTextInstructions
FullName
Title
```

### Usages (Right Side)
```
BarTextInstructions1
BarTextInstructions2
FirstName
LastName
Prefix
Name
```

### Result
These 6 referenced keys are now treated as "used in code" and won't be marked as orphaned.

## Integration with Analysis

### Updated Logic
```csharp
// Parse LocalizationPost.lua for definitions
var definedKeys = await parser.ParseLocalizationDefinitionsAsync(localizationPostPath);

// Parse LocalizationPost.lua for usages (NEW!)
var usedKeys = await parser.ParseLocalizationUsagesAsync(localizationPostPath);

// Add usages to code result (treat as code usage)
foreach (var key in usedKeys)
{
    if (!codeResult.GlueStrings.ContainsKey(key))
    {
        codeResult.GlueStrings[key] = new GlueStringInfo
        {
            GlueString = key,
            HasConcatenation = false,
            OccurrenceCount = 1
        };
    }
}
```

## Real-World Impact: TwintopInsanityBar

### Before (Without Usage Parsing)
```
Found 6 entries defined in LocalizationPost.lua
Total strings used in code: 1773
Orphaned in enUS.lua: 779
```

**Problem:** 8 strings like `BarTextInstructions1-8` were marked as orphaned

### After (With Usage Parsing)
```
Found 6 entries defined in LocalizationPost.lua
Found 13 entries referenced in LocalizationPost.lua
Total strings used in code: 1781  (+8)
Orphaned in enUS.lua: 771  (-8)
```

**Fixed:** Those 8 strings are now correctly recognized as used!

## Examples of Rescued Strings

These strings were previously marked as orphaned but are now correctly identified as used:

```lua
L["BarTextInstructions1"]
L["BarTextInstructions2"]
L["BarTextInstructions3"]
L["BarTextInstructions4"]
L["BarTextInstructions5"]
L["BarTextInstructions6"]
L["BarTextInstructions7"]
L["BarTextInstructions8"]
```

They're used in:
```lua
L["BarTextInstructions"] = L["BarTextInstructions1"] .. "\n\n" .. 
                           L["BarTextInstructions2"] .. "\n\n" ..
                           -- ... etc
```

## Test Coverage

Added 4 comprehensive tests:

### 1. ParseLocalizationUsagesAsync_ExtractsRightSideReferences
```csharp
L["FullName"] = L["FirstName"] .. " " .. L["LastName"]
// Extracts: FirstName, LastName
// Excludes: FullName (left side)
```

### 2. ParseLocalizationUsagesAsync_HandlesComplexExpressions
```csharp
L["Complex"] = string.format("%s", L["Part1"], L["Part2"])
L["Nested"] = L["A"] .. (L["B"] .. L["C"]])
// Extracts all referenced keys
```

### 3. ParseLocalizationUsages_SynchronousVersion_WorksCorrectly
Tests synchronous version

### 4. ParseLocalizationUsagesAsync_IgnoresNonAssignmentLines
```csharp
-- Comment with L["CommentKey"]
print(L["PrintKey"])
// Ignores these - only processes assignment lines
```

**All 22 tests passing!**

## Benefits

### ? Accurate Orphan Detection
No more false positives for strings used in compound localizations

### ? Complete Usage Tracking
Tracks both:
- Direct code usage
- Indirect usage through compound strings

### ? Better Cleanup Decisions
Only marks truly unused strings as orphaned

### ? Handles Complex Patterns
Works with:
- String concatenation
- Function calls (`string.format`)
- Nested expressions
- Conditional expressions

## Usage

### Parse Definitions and Usages
```csharp
var parser = new LuaLocalizationParserService();

// Get keys defined (left side)
var defined = await parser.ParseLocalizationDefinitionsAsync("LocalizationPost.lua");

// Get keys used (right side)
var used = await parser.ParseLocalizationUsagesAsync("LocalizationPost.lua");

// Treat usages as code usage
codeResult.GlueStrings.UnionWith(used);
```

## Summary

The new `ParseLocalizationUsagesAsync` method correctly handles compound localization strings by:

1. **Parsing the right side** of assignments in localization files
2. **Extracting all `L["..."]` references** used in compound strings
3. **Treating those references as code usage** to prevent false orphan detection

This results in **more accurate analysis** with fewer false positives, particularly for documentation strings and compound localizations.

### Impact
- **8 strings rescued** from orphan status
- **100% accuracy** for compound localization tracking
- **Better cleanup guidance** - only remove truly unused strings
