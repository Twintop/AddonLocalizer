using System.Diagnostics;
using Google.Cloud.Translation.V2;
using AddonLocalizer.Core.Interfaces;

namespace AddonLocalizer.Core.Services;

/// <summary>
/// Service for translating text using Google Cloud Translation API
/// </summary>
public class GoogleTranslateService : IGoogleTranslateService
{
    private TranslationClient? _client;
    private string? _jsonKeyFilePath;
    
    // Google API limits: 128 texts per request, 30k characters total
    private const int BatchSize = 100;
    private const int RateLimitDelayMs = 100; // Prevent rate limiting
    private const string JsonKeyFileEnvironmentVariable = "GOOGLE_APPLICATION_CREDENTIALS";
    
    public bool IsConfigured => _client != null && !string.IsNullOrWhiteSpace(_jsonKeyFilePath);

    /// <summary>
    /// Attempts to configure the service from the GOOGLE_APPLICATION_CREDENTIALS environment variable.
    /// Returns true if successful, false if the environment variable is not set.
    /// </summary>
    public bool TryConfigureFromEnvironment()
    {
        var jsonKeyFilePath = Environment.GetEnvironmentVariable(JsonKeyFileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(jsonKeyFilePath))
        {
            Debug.WriteLine($"[GoogleTranslate] Environment variable {JsonKeyFileEnvironmentVariable} not set");
            return false;
        }

        try
        {
            Configure(jsonKeyFilePath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GoogleTranslate] Failed to configure from environment: {ex.Message}");
            return false;
        }
    }

    public void Configure(string jsonKeyFilePath)
    {
        if (string.IsNullOrWhiteSpace(jsonKeyFilePath))
        {
            throw new ArgumentException("JSON key file path cannot be null or empty", nameof(jsonKeyFilePath));
        }

        if (!File.Exists(jsonKeyFilePath))
        {
            throw new FileNotFoundException($"Google Cloud service account JSON key file not found: {jsonKeyFilePath}", jsonKeyFilePath);
        }

        _jsonKeyFilePath = jsonKeyFilePath;
        
        // Create client from the service account credentials file
        var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(jsonKeyFilePath);
        _client = TranslationClient.Create(credential);
        
        Debug.WriteLine($"[GoogleTranslate] Service configured with JSON key file: {jsonKeyFilePath}");
    }
    
    public async Task<string?> TranslateAsync(string text, string targetLanguageCode)
    {
        if (!IsConfigured || _client == null)
        {
            throw new InvalidOperationException($"Google Translate service not configured. Set the {JsonKeyFileEnvironmentVariable} environment variable.");
        }
            
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
            
        try
        {
            var result = await Task.Run(() => _client.TranslateText(text, targetLanguageCode, "en"));
            Debug.WriteLine($"[GoogleTranslate] Translated to {targetLanguageCode}: '{text}' -> '{result.TranslatedText}'");
            return result.TranslatedText;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GoogleTranslate] Translation failed for '{text}': {ex.Message}");
            return null;
        }
    }
    
    public async Task<Dictionary<string, string>> TranslateBatchAsync(
        IEnumerable<string> texts, 
        string targetLanguageCode,
        IProgress<TranslationProgress>? progress = null)
    {
        if (!IsConfigured || _client == null)
        {
            throw new InvalidOperationException($"Google Translate service not configured. Set the {JsonKeyFileEnvironmentVariable} environment variable.");
        }
            
        var textList = texts.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
        var results = new Dictionary<string, string>();
        var totalCount = textList.Count;
        var processedCount = 0;
        
        if (totalCount == 0)
        {
            return results;
        }
        
        Debug.WriteLine($"[GoogleTranslate] Starting batch translation of {totalCount} texts to {targetLanguageCode}");
        
        // Process in batches to respect API limits
        var batches = textList
            .Select((text, index) => new { text, index })
            .GroupBy(x => x.index / BatchSize)
            .Select(g => g.Select(x => x.text).ToList())
            .ToList();
            
        foreach (var batch in batches)
        {
            try
            {
                // Use Task.Run to avoid blocking on synchronous API call
                var translations = await Task.Run(() => 
                    _client.TranslateText(batch, targetLanguageCode, "en"));
                    
                for (var i = 0; i < batch.Count; i++)
                {
                    if (translations[i] != null)
                    {
                        results[batch[i]] = translations[i].TranslatedText;
                    }
                    processedCount++;
                }
                
                progress?.Report(new TranslationProgress
                {
                    ProcessedCount = processedCount,
                    TotalCount = totalCount,
                    CurrentText = batch.LastOrDefault()
                });
                
                Debug.WriteLine($"[GoogleTranslate] Batch complete: {processedCount}/{totalCount}");
                
                // Rate limiting delay between batches
                if (processedCount < totalCount)
                {
                    await Task.Delay(RateLimitDelayMs);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GoogleTranslate] Batch translation error: {ex.Message}");
                
                progress?.Report(new TranslationProgress
                {
                    ProcessedCount = processedCount,
                    TotalCount = totalCount,
                    Error = ex.Message
                });
                
                // Continue with next batch rather than failing completely
            }
        }
        
        Debug.WriteLine($"[GoogleTranslate] Batch translation complete: {results.Count}/{totalCount} succeeded");
        return results;
    }
}
