namespace AddonLocalizer.Core.Interfaces;

public interface IFileSystemService
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    Task<string> ReadAllTextAsync(string path);
    string ReadAllText(string path);
    Task<string[]> ReadAllLinesAsync(string path);
    string[] ReadAllLines(string path);
    Task WriteAllTextAsync(string path, string contents);
    Task WriteAllLinesAsync(string path, IEnumerable<string> lines);
}