using AddonLocalizer.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AddonLocalizer.Models;

public partial class LocalizationEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _glueString;

    [ObservableProperty]
    private int _occurrenceCount;

    [ObservableProperty]
    private bool _hasConcatenation;

    [ObservableProperty]
    private bool _usedInStringFormat;

    [ObservableProperty]
    private int _parameterCount;

    [ObservableProperty]
    private string _fileLocations;

    // Store reference to original data instead of copying everything
    private readonly GlueStringInfo _sourceInfo;

    public LocalizationEntryViewModel(string glueString, GlueStringInfo info)
    {
        _glueString = glueString;
        _occurrenceCount = info.OccurrenceCount;
        _hasConcatenation = info.HasConcatenation;
        _usedInStringFormat = info.UsedInStringFormat;
        _parameterCount = info.ParameterCount;
        _sourceInfo = info;
        
        // Optimized file location summary generation
        _fileLocations = GenerateFileLocationSummary(info);
    }

    private static string GenerateFileLocationSummary(GlueStringInfo info)
    {
        if (info.Locations.Count == 0)
        {
            return "N/A";
        }

        // Use HashSet for efficient distinct file tracking
        var distinctFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var displayFiles = new List<string>(3);
        
        // Single pass through locations
        foreach (var location in info.Locations)
        {
            var fileName = Path.GetFileName(location.FilePath);
            if (distinctFiles.Add(fileName))
            {
                displayFiles.Add(fileName);
            }
        }

        var summary = string.Join(", ", displayFiles);

        return summary;
    }

    // Lazy-load these only when detail view is accessed
    public List<GlueStringLocation> GetLocations() => _sourceInfo.Locations;
    public List<FormatParameter> GetFormatParameters() => _sourceInfo.FormatParameters;

    public string ConcatenationIcon => HasConcatenation ? "X" : "";
    public string StringFormatIcon => UsedInStringFormat ? "X" : "";
}
