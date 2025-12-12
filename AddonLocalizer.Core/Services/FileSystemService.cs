using System.Text;
using AddonLocalizer.Core.Interfaces;

namespace AddonLocalizer.Core.Services;

public class FileSystemService : IFileSystemService
{
    private static readonly Encoding Utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public bool DirectoryExists(string path) => Directory.Exists(path);
        
    public bool FileExists(string path) => File.Exists(path);
        
    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) 
        => Directory.GetFiles(path, searchPattern, searchOption);
        
    public Task<string> ReadAllTextAsync(string path) 
        => File.ReadAllTextAsync(path, Utf8Encoding);
        
    public string ReadAllText(string path) 
        => File.ReadAllText(path, Utf8Encoding);
        
    public Task<string[]> ReadAllLinesAsync(string path) 
        => File.ReadAllLinesAsync(path, Utf8Encoding);
        
    public string[] ReadAllLines(string path) 
        => File.ReadAllLines(path, Utf8Encoding);
    
    public Task WriteAllTextAsync(string path, string contents)
        => File.WriteAllTextAsync(path, contents, Utf8Encoding);
    
    public Task WriteAllLinesAsync(string path, IEnumerable<string> lines)
        => File.WriteAllLinesAsync(path, lines, Utf8Encoding);
}