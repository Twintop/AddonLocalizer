using AddonLocalizer.Core.Services;

var parser = new LuaLocalizationParserService();
var addonPath = @"C:\World of Warcraft\_beta_\Interface\AddOns\TwintopInsanityBar";

try
{
    Console.WriteLine($"Analyzing addon localization: {addonPath}");
    Console.WriteLine(new string('=', 80));
    
    // Parse the main addon code (excluding Localization folder)
    var codeResult = await parser.ParseDirectoryAsync(addonPath, new[] { "Localization" });
    Console.WriteLine($"Found {codeResult.GlueStrings.Count} unique glue strings used in code");
    
    // Count string.format usage
    var stringFormatCount = codeResult.WithStringFormat.Count();
    Console.WriteLine($"Found {stringFormatCount} glue strings used with string.format()");
    
    // Parse enUS.lua - the default localization file
    var enUSPath = Path.Combine(addonPath, "Localization", "enUS.lua");
    HashSet<string> enUSKeys;
    Dictionary<string, List<AddonLocalizer.Core.Models.FormatParameter>> formatParams;
    
    if (File.Exists(enUSPath))
    {
        enUSKeys = await parser.ParseLocalizationDefinitionsAsync(enUSPath);
        Console.WriteLine($"Found {enUSKeys.Count} entries defined in enUS.lua");
        
        // Parse format parameters from enUS.lua
        formatParams = await parser.ParseFormatParametersAsync(enUSPath);
        Console.WriteLine($"Found {formatParams.Count} entries with format parameters in enUS.lua");
    }
    else
    {
        Console.WriteLine("ERROR: enUS.lua not found!");
        return;
    }
    
    // Parse other localization files to know what's already covered
    var localizationPath = Path.Combine(addonPath, "Localization", "Localization.lua");
    var localizationPostPath = Path.Combine(addonPath, "Localization", "LocalizationPost.lua");
    
    var otherLocKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    
    if (File.Exists(localizationPath))
    {
        var keys = await parser.ParseLocalizationDefinitionsAsync(localizationPath);
        otherLocKeys.UnionWith(keys);
        Console.WriteLine($"Found {keys.Count} entries defined in Localization.lua");
    }
    
    if (File.Exists(localizationPostPath))
    {
        var keys = await parser.ParseLocalizationDefinitionsAsync(localizationPostPath);
        otherLocKeys.UnionWith(keys);
        Console.WriteLine($"Found {keys.Count} entries defined in LocalizationPost.lua");
        
        // IMPORTANT: Parse usages (right side) in LocalizationPost.lua
        // These are references to other localization strings, not definitions
        var usedKeys = await parser.ParseLocalizationUsagesAsync(localizationPostPath);
        Console.WriteLine($"Found {usedKeys.Count} entries referenced in LocalizationPost.lua");
        
        // Add these to the code usage since they're being used
        foreach (var key in usedKeys)
        {
            if (!codeResult.GlueStrings.ContainsKey(key))
            {
                codeResult.GlueStrings[key] = new AddonLocalizer.Core.Models.GlueStringInfo
                {
                    GlueString = key,
                    HasConcatenation = false,
                    OccurrenceCount = 1
                };
            }
        }
    }
    
    Console.WriteLine(new string('=', 80));
    
    // Analysis 1: Entries used in code but NOT in enUS.lua
    var missingInEnUS = codeResult.GlueStrings.Keys
        .Where(key => !enUSKeys.Contains(key) && !otherLocKeys.Contains(key))
        .OrderBy(k => k)
        .ToList();
    
    // Analysis 2: Entries in enUS.lua but NOT used in code
    var orphanedInEnUS = enUSKeys
        .Where(key => !codeResult.GlueStrings.ContainsKey(key))
        .OrderBy(k => k)
        .ToList();
    
    // Show results
    Console.WriteLine($"\n?? ANALYSIS SUMMARY");
    Console.WriteLine(new string('-', 80));
    Console.WriteLine($"Total strings used in code: {codeResult.GlueStrings.Count}");
    Console.WriteLine($"With string.format() parameters: {stringFormatCount}");
    Console.WriteLine($"With format specifiers in enUS.lua: {formatParams.Count}");
    Console.WriteLine($"Defined in enUS.lua: {enUSKeys.Count}");
    Console.WriteLine($"Defined in other loc files: {otherLocKeys.Count}");
    Console.WriteLine($"Missing from enUS.lua: {missingInEnUS.Count}");
    Console.WriteLine($"Orphaned in enUS.lua: {orphanedInEnUS.Count}");
    Console.WriteLine(new string('=', 80));
    
    // Show missing entries
    if (missingInEnUS.Any())
    {
        Console.WriteLine($"\n? MISSING IN enUS.lua ({missingInEnUS.Count} entries)");
        Console.WriteLine("These are used in code but have NO localization entry:");
        Console.WriteLine(new string('-', 80));
        
        foreach (var key in missingInEnUS.Take(50))
        {
            var info = codeResult.GlueStrings[key];
            var formatIndicator = info.UsedInStringFormat ? " [NEEDS PARAMS]" : "";
            Console.WriteLine($"  L[\"{key}\"] = \"{key}\"{formatIndicator}  -- Used {info.OccurrenceCount}x");
        }
        
        if (missingInEnUS.Count > 50)
        {
            Console.WriteLine($"  ... and {missingInEnUS.Count - 50} more");
        }
        
        // Write to file for easy copy-paste with parameter indicators
        var outputPath = Path.Combine(addonPath, "missing_enUS_entries.lua");
        var missingEntries = missingInEnUS.Select(key =>
        {
            var info = codeResult.GlueStrings[key];
            var comment = info.UsedInStringFormat ? " -- REQUIRES FORMAT PARAMETERS" : "";
            return $"L[\"{key}\"] = \"{key}\"{comment}";
        });
        await File.WriteAllLinesAsync(outputPath, missingEntries);
        Console.WriteLine($"\n?? Full list written to: {outputPath}");
    }
    else
    {
        Console.WriteLine($"\n? All code strings have enUS.lua entries!");
    }
    
    // Show format parameter details
    if (formatParams.Count > 0)
    {
        var sortedParams = formatParams.OrderBy(kvp => kvp.Key).ToList();
        
        Console.WriteLine($"\n?? FORMAT PARAMETERS ({formatParams.Count} entries)");
        Console.WriteLine("Detected format specifiers in localization strings:");
        Console.WriteLine(new string('-', 80));
        
        foreach (var kvp in sortedParams.Take(30))
        {
            var key = kvp.Key;
            var parameters = kvp.Value;
            var paramCount = parameters.Count(p => p.Type != AddonLocalizer.Core.Models.FormatParameterType.Percent);
            
            var paramTypes = string.Join(", ", parameters
                .Where(p => p.Type != AddonLocalizer.Core.Models.FormatParameterType.Percent)
                .Select(p => $"{p.Type} ({p.RawSpecifier})"));
            
            Console.WriteLine($"  L[\"{key}\"]");
            Console.WriteLine($"    Parameters: {paramCount} - {paramTypes}");
            
            // Show if used in code
            if (codeResult.GlueStrings.TryGetValue(key, out var info) && info.UsedInStringFormat)
            {
                Console.WriteLine($"    ? Used in string.format() {info.StringFormatLocations.Count}x");
            }
        }
        
        if (sortedParams.Count > 30)
        {
            Console.WriteLine($"  ... and {sortedParams.Count - 30} more");
        }
        
        // Write detailed format parameter info to file
        var formatDetailsPath = Path.Combine(addonPath, "format_parameters_detailed.txt");
        var formatDetails = sortedParams.Select(kvp =>
        {
            var key = kvp.Key;
            var parameters = kvp.Value;
            var paramCount = parameters.Count(p => p.Type != AddonLocalizer.Core.Models.FormatParameterType.Percent);
            var paramTypes = string.Join(", ", parameters
                .Where(p => p.Type != AddonLocalizer.Core.Models.FormatParameterType.Percent)
                .Select(p => $"{p.Type} ({p.RawSpecifier})"));
            
            return $"{key}\t{paramCount}\t{paramTypes}";
        });
        await File.WriteAllLinesAsync(formatDetailsPath, 
            new[] { "Key\tParameter Count\tParameter Types" }.Concat(formatDetails));
        Console.WriteLine($"\n?? Detailed format info written to: {formatDetailsPath}");
    }
    
    // Show string.format usage details
    if (stringFormatCount > 0)
    {
        var formatStrings = codeResult.WithStringFormat.OrderBy(g => g.GlueString).ToList();
        
        Console.WriteLine($"\n?? STRING.FORMAT USAGE ({formatStrings.Count} entries)");
        Console.WriteLine("These strings are used with string.format() in code:");
        Console.WriteLine(new string('-', 80));
        
        foreach (var info in formatStrings.Take(20))
        {
            var hasDefinition = formatParams.TryGetValue(info.GlueString, out var parameters);
            var paramInfo = hasDefinition 
                ? $" - Expects {parameters!.Count(p => p.Type != AddonLocalizer.Core.Models.FormatParameterType.Percent)} parameter(s)"
                : " - No parameter info (not in enUS.lua)";
            
            Console.WriteLine($"  L[\"{info.GlueString}\"]{paramInfo}");
            Console.WriteLine($"    Used in string.format() {info.StringFormatLocations.Count}x");
        }
        
        if (formatStrings.Count > 20)
        {
            Console.WriteLine($"  ... and {formatStrings.Count - 20} more");
        }
    }
    
    // Show orphaned entries
    if (orphanedInEnUS.Any())
    {
        Console.WriteLine($"\n??  ORPHANED IN enUS.lua ({orphanedInEnUS.Count} entries)");
        Console.WriteLine("These are defined but NEVER used in code:");
        Console.WriteLine(new string('-', 80));
        
        foreach (var key in orphanedInEnUS.Take(50))
        {
            Console.WriteLine($"  L[\"{key}\"]");
        }
        
        if (orphanedInEnUS.Count > 50)
        {
            Console.WriteLine($"  ... and {orphanedInEnUS.Count - 50} more");
        }
        
        // Write to file for review
        var orphanedPath = Path.Combine(addonPath, "orphaned_enUS_entries.txt");
        await File.WriteAllLinesAsync(orphanedPath, orphanedInEnUS);
        Console.WriteLine($"\n?? Full list written to: {orphanedPath}");
    }
    else
    {
        Console.WriteLine($"\n? No orphaned entries in enUS.lua!");
    }
    
    // Summary with actionable metrics
    Console.WriteLine($"\n?? COVERAGE METRICS");
    Console.WriteLine(new string('-', 80));
    var coverage = ((double)(codeResult.GlueStrings.Count - missingInEnUS.Count) / codeResult.GlueStrings.Count) * 100;
    Console.WriteLine($"enUS.lua coverage: {coverage:F1}% ({codeResult.GlueStrings.Count - missingInEnUS.Count}/{codeResult.GlueStrings.Count})");
    
    if (orphanedInEnUS.Any())
    {
        var cleanupPotential = ((double)orphanedInEnUS.Count / enUSKeys.Count) * 100;
        Console.WriteLine($"Cleanup potential: {cleanupPotential:F1}% ({orphanedInEnUS.Count}/{enUSKeys.Count} can be removed)");
    }
    
    if (stringFormatCount > 0)
    {
        var formatPercentage = ((double)stringFormatCount / codeResult.GlueStrings.Count) * 100;
        Console.WriteLine($"Parameterized strings: {formatPercentage:F1}% ({stringFormatCount}/{codeResult.GlueStrings.Count})");
    }
    
    Console.WriteLine(new string('=', 80));
}
catch (Exception ex)
{
    Console.WriteLine($"\nError: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
