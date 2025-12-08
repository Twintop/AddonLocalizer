namespace AddonLocalizer.Core.Models;

public class GlueStringLocation
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}

public enum FormatParameterType
{
    String,      // %s
    Integer,     // %d, %i
    Float,       // %f
    Character,   // %c
    Unsigned,    // %u
    Hexadecimal, // %x, %X
    Octal,       // %o
    Exponential, // %e, %E
    General,     // %g, %G
    Percent      // %%
}

public class FormatParameter
{
    public int Position { get; set; }  // Position in format string (1-based for positional, 0 for sequential)
    public FormatParameterType Type { get; set; }
    public string RawSpecifier { get; set; } = string.Empty;  // e.g., "%s", "%1$s", "%.2f"
    public int? Width { get; set; }
    public int? Precision { get; set; }
    public bool HasPositionalIndex { get; set; }
}

public class GlueStringInfo
{
    public string GlueString { get; set; } = string.Empty;
    public bool HasConcatenation { get; set; }
    public bool UsedInStringFormat { get; set; }
    public int OccurrenceCount { get; set; }
    public List<GlueStringLocation> Locations { get; set; } = [];
    public List<GlueStringLocation> StringFormatLocations { get; set; } = [];
    public List<FormatParameter> FormatParameters { get; set; } = [];
    
    public int ParameterCount => FormatParameters.Count(p => p.Type != FormatParameterType.Percent);
}

public class ParseResult
{
    public Dictionary<string, GlueStringInfo> GlueStrings { get; set; } = new();
        
    public IEnumerable<GlueStringInfo> NonConcatenated => 
        GlueStrings.Values.Where(g => !g.HasConcatenation);
        
    public IEnumerable<GlueStringInfo> Concatenated => 
        GlueStrings.Values.Where(g => g.HasConcatenation);
    
    public IEnumerable<GlueStringInfo> WithStringFormat =>
        GlueStrings.Values.Where(g => g.UsedInStringFormat);
}