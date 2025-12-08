using AddonLocalizer.Core.Models;

namespace AddonLocalizer.Core.Interfaces;

public interface ILuaLocalizationParserService
{
    Task<ParseResult> ParseDirectoryAsync(string directoryPath, string[]? excludeSubdirectories = null);
    Task<ParseResult> ParseFileAsync(string filePath);
    ParseResult ParseDirectory(string directoryPath, string[]? excludeSubdirectories = null);
    ParseResult ParseFile(string filePath);
    
    /// <summary>
    /// Parses a localization definition file and returns only the keys being assigned (left side of =).
    /// This is useful for parsing Localization.lua files where L["key"] = "value" patterns define translations.
    /// </summary>
    Task<HashSet<string>> ParseLocalizationDefinitionsAsync(string filePath);
    
    /// <summary>
    /// Synchronous version of ParseLocalizationDefinitionsAsync.
    /// </summary>
    HashSet<string> ParseLocalizationDefinitions(string filePath);
    
    /// <summary>
    /// Parses a localization file and returns keys being used (referenced) on the right side of assignments.
    /// This is useful for parsing LocalizationPost.lua where compound strings reference other localization keys.
    /// Example: L["FullName"] = L["FirstName"] .. " " .. L["LastName"]
    /// Returns: ["FirstName", "LastName"]
    /// </summary>
    Task<HashSet<string>> ParseLocalizationUsagesAsync(string filePath);
    
    /// <summary>
    /// Synchronous version of ParseLocalizationUsagesAsync.
    /// </summary>
    HashSet<string> ParseLocalizationUsages(string filePath);
    
    /// <summary>
    /// Parses a localization file and extracts format parameters from string values.
    /// Returns a dictionary mapping localization keys to their format parameter lists.
    /// Example: L["Template"] = "Player %s has %d items" 
    /// Returns: { "Template": [FormatParameter(String), FormatParameter(Integer)] }
    /// </summary>
    Task<Dictionary<string, List<FormatParameter>>> ParseFormatParametersAsync(string filePath);
}