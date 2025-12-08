using AddonLocalizer.Core.Services;

var parser = new LuaLocalizationParserService();
var addonPath = @"C:\World of Warcraft\_beta_\Interface\AddOns\TwintopInsanityBar";

try
{
    Console.WriteLine($"Parsing addon directory: {addonPath}");
    Console.WriteLine("Excluding: Localization subdirectory");
    Console.WriteLine(new string('=', 80));
    
    // Parse excluding the Localization subdirectory
    var result = await parser.ParseDirectoryAsync(addonPath, new[] { "Localization" });
    
    Console.WriteLine($"\nTotal unique glue strings found: {result.GlueStrings.Count}");
    Console.WriteLine(new string('-', 80));
    
    // Show non-concatenated strings
    var nonConcat = result.NonConcatenated.ToList();
    Console.WriteLine($"\nNon-Concatenated Strings ({nonConcat.Count}):");
    Console.WriteLine("(Glue String ? Occurrence Count)");
    Console.WriteLine(new string('-', 80));
    
    foreach (var info in nonConcat.OrderBy(x => x.GlueString).Take(20))
    {
        Console.WriteLine($"  L[\"{info.GlueString}\"] ? {info.OccurrenceCount} occurrence(s)");
    }
    
    if (nonConcat.Count > 20)
    {
        Console.WriteLine($"  ... and {nonConcat.Count - 20} more");
    }
    
    // Show concatenated strings with locations
    var concat = result.Concatenated.ToList();
    if (concat.Any())
    {
        Console.WriteLine($"\nConcatenated Strings ({concat.Count}):");
        Console.WriteLine("(Glue String ? Locations)");
        Console.WriteLine(new string('-', 80));
        
        foreach (var info in concat.OrderBy(x => x.GlueString).Take(10))
        {
            Console.WriteLine($"  L[\"{info.GlueString}\"] ({info.OccurrenceCount} occurrence(s)):");
            foreach (var location in info.Locations.Take(3))
            {
                var fileName = Path.GetFileName(location.FilePath);
                Console.WriteLine($"    - {fileName}:{location.LineNumber}");
            }
            if (info.Locations.Count > 3)
            {
                Console.WriteLine($"    ... and {info.Locations.Count - 3} more locations");
            }
        }
        
        if (concat.Count > 10)
        {
            Console.WriteLine($"  ... and {concat.Count - 10} more concatenated strings");
        }
    }
    else
    {
        Console.WriteLine($"\nConcatenated Strings: None found");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\nError: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
