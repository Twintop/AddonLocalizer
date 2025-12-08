using AddonLocalizer.Core.Models;

namespace AddonLocalizer.Core.Interfaces;

public interface ILuaLocalizationParserService
{
    Task<ParseResult> ParseDirectoryAsync(string directoryPath, string[]? excludeSubdirectories = null);
    Task<ParseResult> ParseFileAsync(string filePath);
    ParseResult ParseDirectory(string directoryPath, string[]? excludeSubdirectories = null);
    ParseResult ParseFile(string filePath);
}