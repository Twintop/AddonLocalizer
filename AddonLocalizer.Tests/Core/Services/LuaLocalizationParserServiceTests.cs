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

    [Fact]
    public async Task ParseLocalizationDefinitionsAsync_OnlyMatchesAssignments()
    {
        var content = new[]
        {
            @"L[""MageFrostFull""] = ""Frost Mage""",
            @"local x = L[""UsageNotDefinition""]",
            @"print(L[""AnotherUsage""])",

            @"L[""ValidAssignment""] = ""Some Value"""
        };
        SetupFileWithLines("localization.lua", content);

        var result = await _parser.ParseLocalizationDefinitionsAsync("localization.lua");

        Assert.Equal(2, result.Count);
        Assert.Contains("MageFrostFull", result);
        Assert.Contains("ValidAssignment", result);
        Assert.DoesNotContain("UsageNotDefinition", result);
        Assert.DoesNotContain("AnotherUsage", result);
    }

    [Fact]
    public async Task ParseLocalizationDefinitionsAsync_HandlesComplexAssignments()
    {
        var content = new[]
        {
            @"L[""SimpleKey""] = ""Simple Value""",
            @"L[""ConcatKey""] = L[""Part1""] .. L[""Part2""]",
            @"L[""FunctionKey""] = string.format(""%s - %s"", L[""A""], L[""B""])",

            @"    L[""IndentedKey""] = ""Indented""",
            @"L[""KeyWithSpaces""]    =    ""Value"""
        };
        SetupFileWithLines("localization.lua", content);

        var result = await _parser.ParseLocalizationDefinitionsAsync("localization.lua");

        // Should only capture the keys being assigned, not the keys used in the values
        Assert.Equal(5, result.Count);
        Assert.Contains("SimpleKey", result);
        Assert.Contains("ConcatKey", result);
        Assert.Contains("FunctionKey", result);
        Assert.Contains("IndentedKey", result);
        Assert.Contains("KeyWithSpaces", result);
        
        // Should NOT contain keys that appear on the right side
        Assert.DoesNotContain("Part1", result);
        Assert.DoesNotContain("Part2", result);
        Assert.DoesNotContain("A", result);
        Assert.DoesNotContain("B", result);
    }

    [Fact]
    public void ParseLocalizationDefinitions_SynchronousVersion_WorksCorrectly()
    {
        var content = new[]
        {
            @"L[""Key1""] = ""Value1""",
            @"L[""Key2""] = ""Value2""",
        };
        SetupFileWithLines("localization.lua", content);

        var result = _parser.ParseLocalizationDefinitions("localization.lua");

        Assert.Equal(2, result.Count);
        Assert.Contains("Key1", result);
        Assert.Contains("Key2", result);
    }

    [Fact]
    public async Task ParseLocalizationDefinitionsAsync_IgnoresNonAssignmentLines()
    {
        var content = new[]
        {
            @"-- Comment with L[""CommentKey""]",
            @"if condition then",
            @"    local x = L[""UsedKey""]",
            @"end",
            @"L[""DefinedKey""] = ""Defined Value""",
            @"return L[""ReturnedKey""]"
        };
        SetupFileWithLines("localization.lua", content);

        var result = await _parser.ParseLocalizationDefinitionsAsync("localization.lua");

        Assert.Single(result);
        Assert.Contains("DefinedKey", result);
        Assert.DoesNotContain("CommentKey", result);
        Assert.DoesNotContain("UsedKey", result);
        Assert.DoesNotContain("ReturnedKey", result);
    }

    [Fact]
    public async Task ParseLocalizationUsagesAsync_ExtractsRightSideReferences()
    {
        var content = new[]
        {
            @"L[""FullName""] = L[""FirstName""] .. "" "" .. L[""LastName""]",
            @"L[""Title""] = L[""Prefix""] .. "": "" .. L[""Name""]",
            @"L[""SimpleKey""] = ""No references here"""
        };
        SetupFileWithLines("localizationPost.lua", content);

        var result = await _parser.ParseLocalizationUsagesAsync("localizationPost.lua");

        Assert.Equal(4, result.Count);
        Assert.Contains("FirstName", result);
        Assert.Contains("LastName", result);
        Assert.Contains("Prefix", result);
        Assert.Contains("Name", result);
        
        // Should NOT contain the keys being defined (left side)
        Assert.DoesNotContain("FullName", result);
        Assert.DoesNotContain("Title", result);
        Assert.DoesNotContain("SimpleKey", result);
    }

    [Fact]
    public async Task ParseLocalizationUsagesAsync_HandlesComplexExpressions()
    {
        var content = new[]
        {
            @"L[""Complex""] = string.format(""%s - %s"", L[""Part1""], L[""Part2""])",

            @"L[""Nested""] = L[""A""] .. (L[""B""] .. L[""C""])",

            @"L[""Conditional""] = condition and L[""Yes""] or L[""No""]"
        };
        SetupFileWithLines("localizationPost.lua", content);

        var result = await _parser.ParseLocalizationUsagesAsync("localizationPost.lua");

        Assert.Equal(7, result.Count);  // Updated to 7 since "No" is also captured
        Assert.Contains("Part1", result);
        Assert.Contains("Part2", result);
        Assert.Contains("A", result);
        Assert.Contains("B", result);
        Assert.Contains("C", result);
        Assert.Contains("Yes", result);
        Assert.Contains("No", result);
    }

    [Fact]
    public void ParseLocalizationUsages_SynchronousVersion_WorksCorrectly()
    {
        var content = new[]
        {
            @"L[""Combined""] = L[""First""] .. L[""Second""]"
        };
        SetupFileWithLines("localizationPost.lua", content);

        var result = _parser.ParseLocalizationUsages("localizationPost.lua");

        Assert.Equal(2, result.Count);
        Assert.Contains("First", result);
        Assert.Contains("Second", result);
        Assert.DoesNotContain("Combined", result);
    }

    [Fact]
    public async Task ParseLocalizationUsagesAsync_IgnoresNonAssignmentLines()
    {
        var content = new[]
        {
            @"-- Comment with L[""CommentKey""]",
            @"L[""Defined""] = L[""UsedKey""]",
            @"print(L[""PrintKey""])"
        };
        SetupFileWithLines("localizationPost.lua", content);

        var result = await _parser.ParseLocalizationUsagesAsync("localizationPost.lua");

        // Only captures from assignment lines
        Assert.Single(result);
        Assert.Contains("UsedKey", result);
        Assert.DoesNotContain("CommentKey", result);
        Assert.DoesNotContain("PrintKey", result);
        Assert.DoesNotContain("Defined", result);
    }

    [Fact]
    public async Task ParseFormatParametersAsync_IgnoresNonAssignmentLines()
    {
        var content = new[]
        {
            @"-- Comment: L[""Comment""] = ""Should ignore %s""",
            @"L[""Valid""] = ""Valid %s""",
            @"print(""Not an assignment %d"")"
        };
        SetupFileWithLines("enUS.lua", content);

        var result = await _parser.ParseFormatParametersAsync("enUS.lua");

        Assert.Single(result);
        Assert.Contains("Valid", result.Keys);
        Assert.DoesNotContain("Comment", result.Keys);
    }

    [Fact]
    public async Task ParseFormatParametersAsync_DoesNotMatchPercentWithSpace()
    {
        var content = new[]
        {
            @"L[""HastePercent""] = ""High Haste% in Voidform""",
            @"L[""ActualFormat""] = ""Player %s has %d items"""
        };
        SetupFileWithLines("enUS.lua", content);

        var result = await _parser.ParseFormatParametersAsync("enUS.lua");

        // HastePercent contains "% i" with space - should NOT be detected as format parameter
        Assert.DoesNotContain("HastePercent", result.Keys);
        
        // ActualFormat has real format parameters
        Assert.Contains("ActualFormat", result.Keys);
        Assert.Equal(2, result["ActualFormat"].Count);
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
