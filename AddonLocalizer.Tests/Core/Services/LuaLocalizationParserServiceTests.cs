namespace AddonLocalizer.Tests.Core.Services;

public class LuaLocalizationParserServiceTests
{
    private readonly Mock<IFileSystemService> _fileSystemMock;
    private readonly LuaLocalizationParserService _parser;

    public LuaLocalizationParserServiceTests()
    {
        _fileSystemMock = new Mock<IFileSystemService>();
        _parser = new LuaLocalizationParserService(_fileSystemMock.Object);
    }

    [Fact]
    public async Task ParseFileAsync_WithSingleLocalizationString_ReturnsCorrectGlueString()
    {
        var content = new[] { """local message = L["HelloWorld"]""" };
        SetupFileWithLines("test.lua", content);

        var result = await _parser.ParseFileAsync("test.lua");

        Assert.Single(result.GlueStrings);
        Assert.Contains("HelloWorld", result.GlueStrings.Keys);
        Assert.Equal(1, result.GlueStrings["HelloWorld"].OccurrenceCount);
        Assert.False(result.GlueStrings["HelloWorld"].HasConcatenation);
    }

    [Fact]
    public async Task ParseFileAsync_WithConcatenationInsideBrackets_MarksAsConcatenated()
    {
        // Concatenation INSIDE the brackets should mark as concatenated
        var content = new[]
        {
            @"local msg1 = L[""Part"" .. myVar]",
            @"local msg2 = L[prefix .. ""Key""]",
            @"local msg3 = L[""Prefix"" .. ""Suffix""]"
        };
        SetupFileWithLines("test.lua", content);

        var result = await _parser.ParseFileAsync("test.lua");

        // Note: The regex will still extract "Part", "Key", "Prefix" and "Suffix" 
        // but the line will be marked as having concatenation
        Assert.True(result.GlueStrings.Values.All(g => g.HasConcatenation));
    }

    [Fact]
    public async Task ParseFileAsync_WithConcatenationOutsideBrackets_DoesNotMarkAsConcatenated()
    {
        // Concatenation outside the brackets should NOT mark as concatenated
        var content = new[]
        {
            @"local author = L[""Author""]",
            @"local fullTitle = L[""Title""] .. myVariable",
            @"local message = ""prefix"" .. L[""Message""] .. ""suffix"""
        };
        SetupFileWithLines("test.lua", content);

        var result = await _parser.ParseFileAsync("test.lua");

        Assert.Equal(3, result.GlueStrings.Count);
        Assert.False(result.GlueStrings["Author"].HasConcatenation);
        Assert.Empty(result.GlueStrings["Author"].Locations);
        Assert.False(result.GlueStrings["Title"].HasConcatenation);
        Assert.Empty(result.GlueStrings["Title"].Locations);
        Assert.False(result.GlueStrings["Message"].HasConcatenation);
        Assert.Empty(result.GlueStrings["Message"].Locations);
    }

    [Fact]
    public async Task ParseFileAsync_ComplexLineWithConcatenation_DetectsCorrectly()
    {
        var content = new[]
        {
            @"local withConcat = L[""Base"" .. variable]",
            @"local withoutConcat = L[""Simple""]",
            @"local outsideConcat = L[""Key""] .. ""text"""
        };
        SetupFileWithLines("test.lua", content);

        var result = await _parser.ParseFileAsync("test.lua");

        // "Base" is in a line with concatenation inside brackets
        Assert.True(result.GlueStrings["Base"].HasConcatenation);
        Assert.Single(result.GlueStrings["Base"].Locations);
        
        // "Simple" has no concatenation
        Assert.False(result.GlueStrings["Simple"].HasConcatenation);
        Assert.Empty(result.GlueStrings["Simple"].Locations);
        
        // "Key" has concatenation outside brackets, which doesn't count
        Assert.False(result.GlueStrings["Key"].HasConcatenation);
        Assert.Empty(result.GlueStrings["Key"].Locations);
    }

    [Fact]
    public async Task ParseFileAsync_StandalonePattern_NotMarkedAsConcatenated()
    {
        var content = new[]
        {
            @"local standalone = L[""Standalone""]",
            @"print(L[""PrintMe""])",
            @"table.insert(array, L[""ArrayItem""])"
        };
        SetupFileWithLines("test.lua", content);

        var result = await _parser.ParseFileAsync("test.lua");

        Assert.Equal(3, result.GlueStrings.Count);
        Assert.All(result.GlueStrings.Values, info => Assert.False(info.HasConcatenation));
        Assert.All(result.GlueStrings.Values, info => Assert.Empty(info.Locations));
    }

    [Fact]
    public async Task ParseFileAsync_NonConcatenated_DoesNotTrackLocations()
    {
        var content = new[]
        {
            @"local simple = L[""SimpleMessage""]",
            @"local another = L[""SimpleMessage""]"
        };
        SetupFileWithLines("test.lua", content);

        var result = await _parser.ParseFileAsync("test.lua");

        var info = result.GlueStrings["SimpleMessage"];
        Assert.False(info.HasConcatenation);
        Assert.Equal(2, info.OccurrenceCount);
        Assert.Empty(info.Locations); // No locations for non-concatenated
    }

    [Fact]
    public async Task ParseFileAsync_ConcatenatedInsideBrackets_TracksAllLocations()
    {
        var content = new[]
        {
            @"local msg1 = L[""ConcatMsg"" .. var1]",
            @"local msg2 = L[""ConcatMsg"" .. var2]",
            @"local msg3 = L[""ConcatMsg"" .. var3]"
        };
        SetupFileWithLines("test.lua", content);

        var result = await _parser.ParseFileAsync("test.lua");

        var info = result.GlueStrings["ConcatMsg"];
        Assert.True(info.HasConcatenation);
        Assert.Equal(3, info.OccurrenceCount);
        Assert.Equal(3, info.Locations.Count);
        Assert.Equal(1, info.Locations[0].LineNumber);
        Assert.Equal(2, info.Locations[1].LineNumber);
        Assert.Equal(3, info.Locations[2].LineNumber);
    }

    [Fact]
    public async Task ParseDirectoryAsync_WithExcludedSubdirectories_SkipsExcludedPaths()
    {
        SetupFileWithLines("main.lua", new[] { @"L[""MainMessage""]" });
        SetupFileWithLines("Localization/enUS.lua", new[] { @"L[""LocalizedString""]" });
        SetupFileWithLines("Localization/deDE.lua", new[] { @"L[""GermanString""]" });
        SetupFileWithLines("Functions/helper.lua", new[] { @"L[""HelperMessage""]" });
        SetupDirectory("testdir", new[] { "main.lua", "Localization/enUS.lua", "Localization/deDE.lua", "Functions/helper.lua" });

        var result = await _parser.ParseDirectoryAsync("testdir", new[] { "Localization" });

        Assert.Equal(2, result.GlueStrings.Count);
        Assert.Contains("MainMessage", result.GlueStrings.Keys);
        Assert.Contains("HelperMessage", result.GlueStrings.Keys);
        Assert.DoesNotContain("LocalizedString", result.GlueStrings.Keys);
        Assert.DoesNotContain("GermanString", result.GlueStrings.Keys);
    }

    private void SetupFileWithLines(string filePath, string[] lines)
    {
        _fileSystemMock.Setup(fs => fs.FileExists(filePath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(filePath)).ReturnsAsync(lines);
        _fileSystemMock.Setup(fs => fs.ReadAllLines(filePath)).Returns(lines);
    }

    private void SetupDirectory(string directoryPath, string[] files)
    {
        _fileSystemMock.Setup(fs => fs.DirectoryExists(directoryPath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.GetFiles(directoryPath, "*.lua", SearchOption.AllDirectories)).Returns(files);
    }
}
