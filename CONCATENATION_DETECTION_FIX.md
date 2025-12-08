# Concatenation Detection Fix

## Problem

The concatenation detection was producing false positives. It was marking glue strings as concatenated if the `..` operator appeared anywhere on the same line, even if it wasn't adjacent to the `L["..."]` pattern.

### Example False Positive
```lua
local author = L["Author"]  -- Later in addon: .. "v1.0.5"
```

The string `L["Author"]` was incorrectly marked as concatenated because `..` appeared later in the line, even though it wasn't part of the same expression.

## Root Cause

The original code used a simple regex pattern to check if `..` appeared anywhere in the line:

```csharp
private static readonly Regex ConcatenationPattern = new(@"\.\.", RegexOptions.Compiled);
bool hasConcatenation = ConcatenationPattern.IsMatch(line);
```

This approach didn't consider the position of `..` relative to the `L["..."]` match.

## Solution

Implemented a new `IsConcatenated` method that checks if `..` is immediately adjacent to the `L["..."]` pattern:

```csharp
private bool IsConcatenated(string line, Match match)
{
    var matchEnd = match.Index + match.Length;
    var matchStart = match.Index;

    // Check for .. after the match (with optional whitespace)
    if (matchEnd < line.Length)
    {
        var afterMatch = line.Substring(matchEnd).TrimStart();
        if (afterMatch.StartsWith(".."))
        {
            return true;
        }
    }

    // Check for .. before the match (with optional whitespace)
    if (matchStart > 0)
    {
        var beforeMatch = line.Substring(0, matchStart).TrimEnd();
        if (beforeMatch.EndsWith(".."))
        {
            return true;
        }
    }

    return false;
}
```

### Algorithm

1. Get the position of the `L["..."]` match in the line
2. Check text immediately after the match (trimming whitespace)
   - If it starts with `..`, mark as concatenated
3. Check text immediately before the match (trimming whitespace)
   - If it ends with `..`, mark as concatenated
4. Otherwise, not concatenated

## Test Coverage

Added 5 new tests to prevent regression:

### 1. False Positive Prevention
```csharp
[Fact]
public async Task ParseFileAsync_WithConcatenationElsewhereInLine_DoesNotMarkAsConcatenated()
```
Tests that `..` elsewhere in the line doesn't cause false positives.

### 2. Adjacent Concatenation Detection
```csharp
[Fact]
public async Task ParseFileAsync_WithConcatenationAdjacentToPattern_MarksAsConcatenated()
```
Verifies that `..` immediately adjacent to `L["..."]` is properly detected (with various whitespace).

### 3. Complex Lines
```csharp
[Fact]
public async Task ParseFileAsync_ComplexLineWithMultipleConcatenations_OnlyMarksAdjacentOnes()
```
Tests lines with multiple `..` operators to ensure only adjacent ones count.

### 4. Standalone Patterns
```csharp
[Fact]
public async Task ParseFileAsync_StandalonePattern_NotMarkedAsConcatenated()
```
Ensures standalone `L["..."]` patterns are never marked as concatenated.

### 5. Parentheses Support
```csharp
[Fact]
public async Task ParseFileAsync_ConcatenationWithParentheses_DetectedCorrectly()
```
Handles concatenation inside parentheses.

## Examples

### ? Correctly Identified as Concatenated
```lua
local msg1 = L["Part1"] .. "text"          -- After
local msg2 = "prefix" .. L["Part2"]        -- Before
local msg3 = L["Part3"]  ..  "text"        -- After with spaces
local msg4 = "prefix"  ..  L["Part4"]      -- Before with spaces
local complex = "a" .. L["Both"] .. "b"    -- Both sides
```

### ? Correctly Identified as NOT Concatenated
```lua
local author = L["Author"]                 -- Standalone
print(L["Message"])                        -- Function argument
local version = "1.0" .. ".5"              -- .. elsewhere in line
table.insert(array, L["Item"])             -- Method call
```

## Test Results

? **All 38 tests passing** (134ms)
- 33 original tests
- 5 new concatenation detection tests

## Impact

This fix eliminates false positives when parsing real addon code. For example, in the TwintopInsanityBar addon:

**Before Fix:**
- Many false positives where `..` appeared elsewhere in complex lines
- Example: `L["Author"]` incorrectly marked as concatenated

**After Fix:**
- Only true concatenations are detected
- Accurate tracking of which strings actually use string concatenation

## Files Modified

1. **AddonLocalizer.Core\Services\LuaLocalizationParserService.cs**
   - Removed global `ConcatenationPattern` regex
   - Added `IsConcatenated(string line, Match match)` method
   - Updated `ProcessLines` to use new detection method

2. **AddonLocalizer.Tests\Core\Services\LuaLocalizationParserServiceTests.cs**
   - Added 5 comprehensive tests for concatenation detection
   - Tests cover various edge cases and prevent regression
