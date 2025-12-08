namespace AddonLocalizer.Core.Models;

public class GlueStringLocation
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}

public class GlueStringInfo
{
    public string GlueString { get; set; } = string.Empty;
    public bool HasConcatenation { get; set; }
    public int OccurrenceCount { get; set; }
    public List<GlueStringLocation> Locations { get; set; } = [];
}

public class ParseResult
{
    public Dictionary<string, GlueStringInfo> GlueStrings { get; set; } = new();
        
    public IEnumerable<GlueStringInfo> NonConcatenated => 
        GlueStrings.Values.Where(g => !g.HasConcatenation);
        
    public IEnumerable<GlueStringInfo> Concatenated => 
        GlueStrings.Values.Where(g => g.HasConcatenation);
}