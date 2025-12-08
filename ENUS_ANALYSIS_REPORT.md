# enUS.lua Analysis Report

## Overview

The `enUS.lua` file is the default English localization file that serves as the base for all translations. This analysis identifies:
1. **Missing entries** - Keys used in code but not defined in `enUS.lua`
2. **Orphaned entries** - Keys defined in `enUS.lua` but never used in code

## Analysis Results

### Summary Statistics
```
Total strings used in code:     1,773
Defined in enUS.lua:            2,272
Defined in other loc files:      116
Missing from enUS.lua:            166
Orphaned in enUS.lua:             779
```

### Coverage Metrics
- **enUS.lua coverage:** 90.6% (1,607/1,773 strings localized)
- **Cleanup potential:** 34.3% (779/2,272 entries can be removed)

## Problem 1: Missing Entries (166)

These 166 strings are **used in the addon code** but have **NO entry in enUS.lua**.

### Impact
- Missing translations for English users
- Will show as raw key names in the UI
- Need to be added to `enUS.lua`

### Examples
```lua
L["AbilityThresholdLinesHeader"] = "AbilityThresholdLinesHeader"  -- Used 19x
L["AddNewBarTextArea"] = "AddNewBarTextArea"  -- Used 1x
L["ArcaneCharge1"] = "ArcaneCharge1"  -- Used 2x
L["AudioChannel"] = "AudioChannel"  -- Used 1x
L["AudioChannelAmbience"] = "AudioChannelAmbience"  -- Used 2x
```

### Output File
All 166 missing entries have been written to:
```
C:\World of Warcraft\_beta_\Interface\AddOns\TwintopInsanityBar\missing_enUS_entries.lua
```

This file can be directly edited with proper English translations and then merged into `enUS.lua`.

### Action Required
1. Review `missing_enUS_entries.lua`
2. Replace placeholder values with proper English text
3. Add entries to `enUS.lua`

**Example transformation:**
```lua
-- Generated placeholder
L["AbilityThresholdLinesHeader"] = "AbilityThresholdLinesHeader"

-- Proper translation
L["AbilityThresholdLinesHeader"] = "Ability Threshold Lines"
```

## Problem 2: Orphaned Entries (779)

These 779 strings are **defined in enUS.lua** but **NEVER used** in the addon code.

### Impact
- Bloat in the localization file
- Confusion for translators
- Maintenance overhead
- Wasted memory at runtime

### Examples
```lua
L["Abilities"]                              -- No usage found
L["AeratedManaPotion"]                      -- No usage found
L["BarEndcapHeader"]                        -- No usage found
L["BarTextInstructions1"]                   -- No usage found
L["BorderColorInnervate"]                   -- No usage found
```

### Categories of Orphaned Entries

#### 1. Old/Removed Features
Likely from features that were removed or refactored:
```lua
L["BarTextInstructions1"] through L["BarTextInstructions8"]
L["CheckboxEndCapEnabled"]
L["CastingBarTexture"]
```

#### 2. Renamed Keys
Keys that may have been renamed in the code:
```lua
L["BorderColorOvercap"]
L["ColorPickerCurrentEnergyAboveOvercap"]
```

#### 3. Class-Specific Removals
Features removed from specific classes:
```lua
L["DemonHunterDevourerBarTextVariable_fury"]
L["DemonHunterDevourerBarTextVariable_furyMax"]
```

#### 4. Item/Spell Names
WoW items/spells that may no longer be relevant:
```lua
L["AeratedManaPotion"]
L["AlgariManaPotion"]
L["CavedwellersDelight"]
```

### Output File
All 779 orphaned entries have been written to:
```
C:\World of Warcraft\_beta_\Interface\AddOns\TwintopInsanityBar\orphaned_enUS_entries.txt
```

### Action Required
1. Review `orphaned_enUS_entries.txt`
2. Verify entries are truly unused
3. Remove from `enUS.lua`
4. Remove from all other localization files (`deDE.lua`, `frFR.lua`, etc.)

## Recommendations

### Immediate Actions

#### 1. Add Missing Entries (High Priority)
**Goal:** Reach 100% coverage

**Steps:**
1. Open `missing_enUS_entries.lua`
2. For each entry, write proper English text
3. Add to `enUS.lua`

**Impact:** Fixes 166 untranslated strings

#### 2. Clean Up Orphaned Entries (Medium Priority)
**Goal:** Reduce file size by 34.3%

**Steps:**
1. Review `orphaned_enUS_entries.txt`
2. Confirm entries are not used
3. Remove from `enUS.lua`
4. Remove from all language files

**Impact:** Cleaner codebase, easier maintenance

### Long-Term Process

#### Continuous Monitoring
Run this analysis regularly to catch:
- New strings added to code without translations
- Old strings removed from code but still in localization files

#### Pre-Release Checklist
Before each release:
```bash
dotnet run --project ParserTest\ParserTest.csproj
```

Verify:
- [ ] Missing entries = 0
- [ ] Orphaned entries reviewed

#### Translation Workflow
1. Developer adds `L["NewKey"]` to code
2. Run analysis to detect missing entry
3. Add English translation to `enUS.lua`
4. Translators update their language files

## Technical Details

### How It Works

#### Missing Entry Detection
```csharp
// Parse all L["..."] patterns used in code
var codeResult = await parser.ParseDirectoryAsync(addonPath, new[] { "Localization" });

// Parse enUS.lua for L["..."] = ... assignments
var enUSKeys = await parser.ParseLocalizationDefinitionsAsync(enUSPath);

// Find keys used in code but not in enUS.lua
var missing = codeResult.GlueStrings.Keys.Where(key => !enUSKeys.Contains(key));
```

#### Orphaned Entry Detection
```csharp
// Find keys in enUS.lua that are not used in code
var orphaned = enUSKeys.Where(key => !codeResult.GlueStrings.ContainsKey(key));
```

### Files Generated

1. **missing_enUS_entries.lua**
   - Ready-to-edit Lua format
   - Can be merged into `enUS.lua`
   - Contains placeholder values

2. **orphaned_enUS_entries.txt**
   - Simple list format
   - For review and removal
   - One key per line

## Examples from Your Addon

### High-Usage Missing Entries
These are used frequently but have no translation:
```lua
L["AbilityThresholdLinesHeader"]              -- Used 19x
L["AudioOptionsHeader"]                       -- Used 40x
L["BarDisplayTextCustomizationHeader"]        -- Used 40x
L["BarTextVariableAgility"]                   -- Used 78x
L["BarTextVariableCrit"]                      -- Used 80x
```

### Likely Safe to Remove
These look like old feature remnants:
```lua
L["BarTextInstructions1"] through L["BarTextInstructions8"]
L["CheckboxEndCapEnabled"]
L["BorderColorInnervate"]
L["CastingBarTexture"]
```

## Summary

Your `enUS.lua` file needs:
- ? **90.6% coverage** - Pretty good!
- ? **166 missing entries** - Need to add proper English text
- ?? **779 orphaned entries** - Cleanup opportunity

**Next Steps:**
1. Add the 166 missing translations to reach 100% coverage
2. Review and remove the 779 orphaned entries to reduce bloat
3. Integrate this analysis into your development workflow

This will result in:
- Complete English localization
- 34% smaller localization files
- Easier maintenance for translators
- Better user experience
