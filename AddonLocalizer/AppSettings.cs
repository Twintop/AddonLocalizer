namespace AddonLocalizer;

/// <summary>
/// Application settings loaded from appsettings.json
/// </summary>
public class AppSettings
{
    /// <summary>
    /// The GitHub repository URL for the addon project.
    /// Used in auto-generated GT file headers to direct contributors.
    /// </summary>
    public string GitHubRepositoryUrl { get; set; } = "https://github.com/Twintop/TwintopInsanityBar";
}
