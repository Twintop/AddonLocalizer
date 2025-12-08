using AddonLocalizer.Core.Services;

var parser = new LuaLocalizationParserService();
var filePath = @"C:\World of Warcraft\_beta_\Interface\AddOns\TwintopInsanityBar\Functions\LibSharedMedia.lua";

try
{
    Console.WriteLine($"Parsing file: {filePath}");
    Console.WriteLine(new string('=', 80));
    
    var result = await parser.ParseFileAsync(filePath);
    
    Console.WriteLine($"\nTotal unique glue strings found: {result.GlueStrings.Count}");
    Console.WriteLine(new string('-', 80));
    
    // Show non-concatenated strings
    var nonConcat = result.NonConcatenated.ToList();
    Console.WriteLine($"\nNon-Concatenated Strings ({nonConcat.Count}):");
    Console.WriteLine("(Glue String ? Occurrence Count)");
    Console.WriteLine(new string('-', 80));
    
    foreach (var info in nonConcat.OrderBy(x => x.GlueString))
    {
        Console.WriteLine($"  L[\"{info.GlueString}\"] ? {info.OccurrenceCount} occurrence(s)");
    }
    
    // Show concatenated strings with locations
    var concat = result.Concatenated.ToList();
    if (concat.Any())
    {
        Console.WriteLine($"\nConcatenated Strings ({concat.Count}):");
        Console.WriteLine("(Glue String ? Locations)");
        Console.WriteLine(new string('-', 80));
        
        foreach (var info in concat.OrderBy(x => x.GlueString))
        {
            Console.WriteLine($"  L[\"{info.GlueString}\"] ({info.OccurrenceCount} occurrence(s)):");
            foreach (var location in info.Locations)
            {
                Console.WriteLine($"    - Line {location.LineNumber}");
            }
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
}
