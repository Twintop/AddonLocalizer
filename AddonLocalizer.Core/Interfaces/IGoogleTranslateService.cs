namespace AddonLocalizer.Core.Interfaces;

/// <summary>
/// Service for translating text using Google Translate API
/// </summary>
public interface IGoogleTranslateService
{
    /// <summary>
    /// Translates a single string from English to target language
    /// </summary>
    /// <param name="text">The text to translate</param>
    /// <param name="targetLanguageCode">Google language code (e.g., "de", "fr", "zh-CN")</param>
    /// <returns>Translated text or null if translation failed</returns>
    Task<string?> TranslateAsync(string text, string targetLanguageCode);
    
    /// <summary>
    /// Translates multiple strings in batch (more efficient for API usage)
    /// </summary>
    /// <param name="texts">The texts to translate</param>
    /// <param name="targetLanguageCode">Google language code</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <returns>Dictionary mapping original text to translated text</returns>
    Task<Dictionary<string, string>> TranslateBatchAsync(
        IEnumerable<string> texts, 
        string targetLanguageCode,
        IProgress<TranslationProgress>? progress = null);
    
    /// <summary>
    /// Check if the service is configured with valid credentials
    /// </summary>
    bool IsConfigured { get; }
    
    /// <summary>
    /// Configure the service with a service account JSON key file
    /// </summary>
    /// <param name="jsonKeyFilePath">Path to the Google Cloud service account JSON key file</param>
    void Configure(string jsonKeyFilePath);

    /// <summary>
    /// Attempts to configure the service from the GOOGLE_APPLICATION_CREDENTIALS environment variable.
    /// </summary>
    /// <returns>True if successful, false if the environment variable is not set or configuration failed</returns>
    bool TryConfigureFromEnvironment();
}

/// <summary>
/// Progress information for batch translation operations
/// </summary>
public class TranslationProgress
{
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public string? CurrentText { get; set; }
    public string? Error { get; set; }
    public bool IsComplete => ProcessedCount >= TotalCount;
}
