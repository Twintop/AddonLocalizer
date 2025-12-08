using AddonLocalizer.Core.Interfaces;

namespace AddonLocalizer.Core.Services;

public class FileSystemService : IFileSystemService
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
        
    public bool FileExists(string path) => File.Exists(path);
        
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) 
        => Directory.GetFiles(path, searchPattern, searchOption);
        
    public Task<string> ReadAllTextAsync(string path) 
        => File.ReadAllTextAsync(path);
        
    public string ReadAllText(string path) 
        => File.ReadAllText(path);
        
    public Task<string[]> ReadAllLinesAsync(string path) 
        => File.ReadAllLinesAsync(path);
        
    public string[] ReadAllLines(string path) 
        => File.ReadAllLines(path);
}