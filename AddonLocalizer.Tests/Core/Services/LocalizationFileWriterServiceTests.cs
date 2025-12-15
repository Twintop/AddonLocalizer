using AddonLocalizer.Core.Models;

namespace AddonLocalizer.Tests.Core.Services;

public class LocalizationFileWriterServiceTests
{
    private readonly Mock<IFileSystemService> _fileSystemMock;
    private readonly LocalizationFileWriterService _writer;

    public LocalizationFileWriterServiceTests()
    {
        _fileSystemMock = new Mock<IFileSystemService>();
        _writer = new LocalizationFileWriterService(_fileSystemMock.Object);
    }

    #region SaveLocaleFileAsync Tests

    [Fact]
    public async Task SaveLocaleFileAsync_WithInvalidDirectory_ThrowsDirectoryNotFoundException()
    {
        _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _writer.SaveLocaleFileAsync("invalid/path", "enUS", new Dictionary<string, string>()));
    }

    [Fact]
    public async Task SaveLocaleFileAsync_WithInvalidLocaleCode_ThrowsArgumentException()
    {
        _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _writer.SaveLocaleFileAsync("/localization", "invalidLocale", new Dictionary<string, string>()));
    }

    [Fact]
    public async Task SaveLocaleFileAsync_NewFile_CreatesFileWithCorrectStructure()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "deDE";
        var translations = new Dictionary<string, string>
        {
            ["HelloWorld"] = "Hallo Welt",
            ["GoodBye"] = "Auf Wiedersehen"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        Assert.Contains("local _, TRB = ...", capturedLines);
        Assert.Contains("if locale == \"deDE\" then", capturedLines);
        Assert.Contains("    local L = TRB.Localization", capturedLines);
        Assert.Contains("    L[\"GoodBye\"] = \"Auf Wiedersehen\"", capturedLines);
        Assert.Contains("    L[\"HelloWorld\"] = \"Hallo Welt\"", capturedLines);
        Assert.Contains("end", capturedLines);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_NewFile_SortsTranslationsAlphabetically()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";
        var translations = new Dictionary<string, string>
        {
            ["Zebra"] = "Last alphabetically",
            ["Apple"] = "First alphabetically",
            ["Middle"] = "Middle alphabetically"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        var translationLines = capturedLines.Where(l => l.Contains("L[\"")).ToList();
        Assert.Equal(3, translationLines.Count);
        Assert.Contains("Apple", translationLines[0]);
        Assert.Contains("Middle", translationLines[1]);
        Assert.Contains("Zebra", translationLines[2]);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_ExistingFile_CreatesBackupWhenRequested()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "frFR";
        var filePath = Path.Combine(localizationDir, $"{localeCode}.lua");
        var existingContent = "existing content";

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(filePath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllTextAsync(filePath)).ReturnsAsync(existingContent);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(filePath)).ReturnsAsync(Array.Empty<string>());
        
        string? backupPath = null;
        _fileSystemMock.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((path, content) => 
            {
                if (path.Contains(".backup"))
                {
                    backupPath = path;
                }
            })
            .Returns(Task.CompletedTask);
        
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, new Dictionary<string, string>(), createBackup: true);

        Assert.NotNull(backupPath);
        Assert.Contains(".backup", backupPath);
        Assert.Contains(localeCode, backupPath);
        
        _fileSystemMock.Verify(fs => fs.WriteAllTextAsync(
            It.Is<string>(s => s.Contains(".backup")),
            existingContent), Times.Once);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_ExistingFile_SkipsBackupWhenNotRequested()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "frFR";
        var filePath = "/addon/Localization/frFR.lua";

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(filePath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(filePath)).ReturnsAsync(Array.Empty<string>());
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, new Dictionary<string, string>(), createBackup: false);

        _fileSystemMock.Verify(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_ExistingFile_PreservesStructure()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "esES";
        var filePath = "/addon/Localization/esES.lua";
        var existingLines = new[]
        {
            "local _, TRB = ...",
            "",
            "local locale = GetLocale()",
            "",
            "if locale == \"esES\" then",
            "    local L = TRB.Localization",
            "    ",
            "    L[\"ExistingKey\"] = \"Valor Existente\"",
            "    L[\"ToUpdate\"] = \"Old Value\"",
            "end",
            ""
        };

        var newTranslations = new Dictionary<string, string>
        {
            ["ExistingKey"] = "Valor Existente",
            ["ToUpdate"] = "New Value",
            ["NewKey"] = "Nuevo Valor"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(filePath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(filePath)).ReturnsAsync(existingLines);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, newTranslations, createBackup: false);

        Assert.NotNull(capturedLines);
        Assert.Contains("local _, TRB = ...", capturedLines);
        Assert.Contains("    L[\"ExistingKey\"] = \"Valor Existente\"", capturedLines);
        Assert.Contains("    L[\"ToUpdate\"] = \"New Value\"", capturedLines);
        Assert.Contains("    L[\"NewKey\"] = \"Nuevo Valor\"", capturedLines);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_ExistingFile_UpdatesExistingTranslations()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "itIT";
        var filePath = "/addon/Localization/itIT.lua";
        var existingLines = new[]
        {
            "local _, TRB = ...",
            "",
            "if locale == \"itIT\" then",
            "    local L = TRB.Localization",
            "    L[\"Key1\"] = \"Old Translation\"",
            "end"
        };

        var newTranslations = new Dictionary<string, string>
        {
            ["Key1"] = "New Translation"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(filePath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(filePath)).ReturnsAsync(existingLines);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, newTranslations, createBackup: false);

        Assert.NotNull(capturedLines);
        Assert.Contains("    L[\"Key1\"] = \"New Translation\"", capturedLines);
        Assert.DoesNotContain("Old Translation", capturedLines);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_ExistingFile_AddsNewKeysAtEnd()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "koKR";
        var filePath = "/addon/Localization/koKR.lua";
        var existingLines = new[]
        {
            "local _, TRB = ...",
            "if locale == \"koKR\" then",
            "    local L = TRB.Localization",
            "    L[\"Existing\"] = \"기존\"",
            "end"
        };

        var newTranslations = new Dictionary<string, string>
        {
            ["Existing"] = "기존",
            ["NewKey"] = "새로운"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(filePath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(filePath)).ReturnsAsync(existingLines);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, newTranslations, createBackup: false);

        Assert.NotNull(capturedLines);
        var newKeyLine = capturedLines.FirstOrDefault(l => l.Contains("NewKey"));
        var existingKeyLine = capturedLines.FirstOrDefault(l => l.Contains("Existing"));
        var endLine = capturedLines.FirstOrDefault(l => l.Trim() == "end");

        Assert.NotNull(newKeyLine);
        Assert.NotNull(existingKeyLine);
        Assert.NotNull(endLine);

        var newKeyIndex = capturedLines.IndexOf(newKeyLine);
        var endIndex = capturedLines.IndexOf(endLine);
        Assert.True(newKeyIndex < endIndex, "New key should appear before 'end'");
    }

    [Fact]
    public async Task SaveLocaleFileAsync_ExistingFile_RemovesKeysWithEmptyValues()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "ptBR";
        var filePath = "/addon/Localization/ptBR.lua";
        var existingLines = new[]
        {
            "local _, TRB = ...",
            "if locale == \"ptBR\" then",
            "    local L = TRB.Localization",
            "    L[\"KeepThis\"] = \"Manter\"",
            "    L[\"RemoveThis\"] = \"Remover\"",
            "end"
        };

        var newTranslations = new Dictionary<string, string>
        {
            ["KeepThis"] = "Manter",
            ["RemoveThis"] = "" // Empty string should remove this key
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(filePath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(filePath)).ReturnsAsync(existingLines);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, newTranslations, createBackup: false);

        Assert.NotNull(capturedLines);
        Assert.Contains("    L[\"KeepThis\"] = \"Manter\"", capturedLines);
        Assert.DoesNotContain("RemoveThis", capturedLines);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_EscapesSpecialCharacters()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";
        var translations = new Dictionary<string, string>
        {
            ["Quotes"] = "He said \"Hello\"",
            ["Backslash"] = "Path: C:\\Users\\Name",
            ["Newline"] = "Line1\nLine2",
            ["Tab"] = "Col1\tCol2"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        Assert.Contains("    L[\"Backslash\"] = \"Path: C:\\\\Users\\\\Name\"", capturedLines);
        Assert.Contains("    L[\"Newline\"] = \"Line1\\nLine2\"", capturedLines);
        Assert.Contains("    L[\"Quotes\"] = \"He said \\\"Hello\\\"\"", capturedLines);
        Assert.Contains("    L[\"Tab\"] = \"Col1\\tCol2\"", capturedLines);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_SkipsEmptyTranslationsInNewFile()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "ruRU";
        var translations = new Dictionary<string, string>
        {
            ["ValidKey"] = "Правильный",
            ["EmptyKey"] = "",
            ["NullKey"] = null!,
            ["WhitespaceKey"] = "   "
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        Assert.Contains("    L[\"ValidKey\"] = \"Правильный\"", capturedLines);
        Assert.DoesNotContain("EmptyKey", capturedLines);
        Assert.DoesNotContain("NullKey", capturedLines);
        Assert.DoesNotContain("WhitespaceKey", capturedLines);
    }

    #endregion

    #region SaveMultipleLocaleFilesAsync Tests

    [Fact]
    public async Task SaveMultipleLocaleFilesAsync_WithInvalidDirectory_ThrowsDirectoryNotFoundException()
    {
        _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        var localeTranslations = new Dictionary<string, Dictionary<string, string>>
        {
            ["enUS"] = new Dictionary<string, string> { ["Key"] = "Value" }
        };

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _writer.SaveMultipleLocaleFilesAsync("invalid/path", localeTranslations));
    }

    [Fact]
    public async Task SaveMultipleLocaleFilesAsync_SavesAllLocales()
    {
        var localizationDir = "/addon/Localization";
        var localeTranslations = new Dictionary<string, Dictionary<string, string>>
        {
            ["enUS"] = new Dictionary<string, string> { ["Hello"] = "Hello" },
            ["deDE"] = new Dictionary<string, string> { ["Hello"] = "Hallo" },
            ["frFR"] = new Dictionary<string, string> { ["Hello"] = "Bonjour" }
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Task.CompletedTask);

        await _writer.SaveMultipleLocaleFilesAsync(localizationDir, localeTranslations);

        _fileSystemMock.Verify(fs => fs.WriteAllLinesAsync(
            It.Is<string>(s => s.EndsWith("enUS.lua")),
            It.IsAny<IEnumerable<string>>()), Times.Once);
        _fileSystemMock.Verify(fs => fs.WriteAllLinesAsync(
            It.Is<string>(s => s.EndsWith("deDE.lua")),
            It.IsAny<IEnumerable<string>>()), Times.Once);
        _fileSystemMock.Verify(fs => fs.WriteAllLinesAsync(
            It.Is<string>(s => s.EndsWith("frFR.lua")),
            It.IsAny<IEnumerable<string>>()), Times.Once);
    }

    [Fact]
    public async Task SaveMultipleLocaleFilesAsync_ReportsProgressCorrectly()
    {
        var localizationDir = "/addon/Localization";
        var localeTranslations = new Dictionary<string, Dictionary<string, string>>
        {
            ["enUS"] = new Dictionary<string, string> { ["Key1"] = "Value1" },
            ["deDE"] = new Dictionary<string, string> { ["Key2"] = "Value2" }
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Task.CompletedTask);

        var progressReports = new List<SaveProgress>();
        var progress = new Progress<SaveProgress>(p => 
        {
            lock (progressReports)
            {
                progressReports.Add(p);
            }
        });

        await _writer.SaveMultipleLocaleFilesAsync(localizationDir, localeTranslations, progress: progress);

        // Wait a moment for progress reports to complete
        await Task.Delay(200);

        // Should report progress for each locale (at least 2 reports)
        Assert.True(progressReports.Count >= 2, $"Expected at least 2 progress reports, got {progressReports.Count}");
        Assert.All(progressReports, p => Assert.Equal(2, p.TotalCount));
        
        // Find the last report
        var lastReport = progressReports[^1];
        Assert.Equal(2, lastReport.ProcessedCount);
        Assert.True(lastReport.IsComplete);
        
        // Verify progress is incremental
        Assert.Contains(progressReports, p => p.ProcessedCount == 1);
        Assert.Contains(progressReports, p => p.ProcessedCount == 2);
    }

    [Fact]
    public async Task SaveMultipleLocaleFilesAsync_ReportsProgressWithCorrectPercentages()
    {
        var localizationDir = "/addon/Localization";
        var localeTranslations = new Dictionary<string, Dictionary<string, string>>
        {
            ["enUS"] = new Dictionary<string, string> { ["Key"] = "Value" },
            ["deDE"] = new Dictionary<string, string> { ["Key"] = "Value" }
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Task.CompletedTask);

        var progressReports = new List<SaveProgress>();
        var progress = new Progress<SaveProgress>(p => progressReports.Add(p));

        await _writer.SaveMultipleLocaleFilesAsync(localizationDir, localeTranslations, progress: progress);

        Assert.Equal(50.0, progressReports[0].PercentComplete);
        Assert.Equal(100.0, progressReports[1].PercentComplete);
    }

    [Fact]
    public async Task SaveMultipleLocaleFilesAsync_ReportsErrorOnFailure()
    {
        var localizationDir = "/addon/Localization";
        var localeTranslations = new Dictionary<string, Dictionary<string, string>>
        {
            ["enUS"] = new Dictionary<string, string> { ["Key"] = "Value" },
            ["deDE"] = new Dictionary<string, string> { ["Key"] = "Value" }
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.Is<string>(s => s.Contains("deDE")), It.IsAny<IEnumerable<string>>()))
            .ThrowsAsync(new IOException("Disk full"));
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.Is<string>(s => s.Contains("enUS")), It.IsAny<IEnumerable<string>>()))
            .Returns(Task.CompletedTask);

        var progressReports = new List<SaveProgress>();
        var progress = new Progress<SaveProgress>(p => progressReports.Add(p));

        await Assert.ThrowsAsync<IOException>(() =>
            _writer.SaveMultipleLocaleFilesAsync(localizationDir, localeTranslations, progress: progress));

        var errorReport = progressReports.FirstOrDefault(p => p.Error != null);
        Assert.NotNull(errorReport);
        Assert.Equal("Disk full", errorReport.Error);
    }

    [Fact]
    public async Task SaveMultipleLocaleFilesAsync_CreatesBackupsWhenRequested()
    {
        var localizationDir = "/addon/Localization";
        var localeTranslations = new Dictionary<string, Dictionary<string, string>>
        {
            ["enUS"] = new Dictionary<string, string> { ["Key"] = "Value" }
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync("existing");
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(It.IsAny<string>())).ReturnsAsync(Array.Empty<string>());
        _fileSystemMock.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Task.CompletedTask);

        await _writer.SaveMultipleLocaleFilesAsync(localizationDir, localeTranslations, createBackup: true);

        _fileSystemMock.Verify(fs => fs.WriteAllTextAsync(
            It.Is<string>(s => s.Contains(".backup")),
            "existing"), Times.Once);
    }

    [Fact]
    public async Task SaveMultipleLocaleFilesAsync_SkipsBackupsWhenNotRequested()
    {
        var localizationDir = "/addon/Localization";
        var localeTranslations = new Dictionary<string, Dictionary<string, string>>
        {
            ["enUS"] = new Dictionary<string, string> { ["Key"] = "Value" }
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(It.IsAny<string>())).ReturnsAsync(Array.Empty<string>());
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Task.CompletedTask);

        await _writer.SaveMultipleLocaleFilesAsync(localizationDir, localeTranslations, createBackup: false);

        _fileSystemMock.Verify(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region DeleteBackupsAsync Tests

    [Fact]
    public async Task DeleteBackupsAsync_DeletesAllBackupFiles()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";
        var backupFiles = new[]
        {
            "/addon/Localization/enUS.lua.20240101_120000.backup",
            "/addon/Localization/enUS.lua.20240101_130000.backup",
            "/addon/Localization/enUS.lua.20240101_140000.backup"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock.Setup(fs => fs.GetFiles(It.IsAny<string>(), It.Is<string>(s => s.Contains("backup")), SearchOption.TopDirectoryOnly))
            .Returns(backupFiles);

        await _writer.DeleteBackupsAsync(localizationDir, localeCode);

        // Verify that each backup file was deleted
        foreach (var backupFile in backupFiles)
        {
            _fileSystemMock.Verify(fs => fs.DeleteFile(backupFile), Times.Once);
        }
        
        // Verify that GetFiles was called to find backups
        _fileSystemMock.Verify(fs => fs.GetFiles(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains(".backup")),
            SearchOption.TopDirectoryOnly), Times.Once);
    }

    [Fact]
    public async Task DeleteBackupsAsync_HandlesNonExistentDirectory()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";

        _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        // Should not throw
        await _writer.DeleteBackupsAsync(localizationDir, localeCode);
        
        // Should not attempt to get files or delete anything
        _fileSystemMock.Verify(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Never);
        _fileSystemMock.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteBackupsAsync_HandlesNoBackupFiles()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";

        _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock.Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>(), SearchOption.TopDirectoryOnly))
            .Returns(Array.Empty<string>());

        // Should not throw
        await _writer.DeleteBackupsAsync(localizationDir, localeCode);
        
        // Should call GetFiles but not DeleteFile
        _fileSystemMock.Verify(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>(), SearchOption.TopDirectoryOnly), Times.Once);
        _fileSystemMock.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region RestoreFromBackupAsync Tests

    [Fact]
    public async Task RestoreFromBackupAsync_WithNoBackups_ThrowsFileNotFoundException()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";

        _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock.Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>(), SearchOption.TopDirectoryOnly))
            .Returns(Array.Empty<string>());

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _writer.RestoreFromBackupAsync(localizationDir, localeCode));
    }

    [Fact]
    public async Task RestoreFromBackupAsync_RestoresMostRecentBackup()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";
        var backupFiles = new[]
        {
            "/addon/Localization/enUS.lua.20240101_120000.backup",
            "/addon/Localization/enUS.lua.20240101_140000.backup", // Most recent
            "/addon/Localization/enUS.lua.20240101_130000.backup"
        };
        var backupContent = "backup content";

        _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock.Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>(), SearchOption.TopDirectoryOnly))
            .Returns(backupFiles);
        _fileSystemMock.Setup(fs => fs.ReadAllTextAsync(backupFiles[1])).ReturnsAsync(backupContent);
        
        // The implementation uses Path.Combine which may produce backslashes on Windows
        _fileSystemMock.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), backupContent))
            .Returns(Task.CompletedTask);

        await _writer.RestoreFromBackupAsync(localizationDir, localeCode);

        _fileSystemMock.Verify(fs => fs.ReadAllTextAsync(backupFiles[1]), Times.Once);
        _fileSystemMock.Verify(fs => fs.WriteAllTextAsync(It.IsAny<string>(), backupContent), Times.Once);
    }

    [Fact]
    public async Task RestoreFromBackupAsync_WithInvalidDirectory_ThrowsDirectoryNotFoundException()
    {
        var localizationDir = "/invalid/path";
        var localeCode = "enUS";

        _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            _writer.RestoreFromBackupAsync(localizationDir, localeCode));
    }

    #endregion

    #region Edge Cases and Special Scenarios

    [Fact]
    public async Task SaveLocaleFileAsync_WithComplexCharacters_HandlesCorrectly()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "zhCN";
        var translations = new Dictionary<string, string>
        {
            ["Chinese"] = "中文测试",
            ["Japanese"] = "日本語テスト",
            ["Emoji"] = "Test 😀 🎮",
            ["Mixed"] = "Mixed 中文 English 日本語"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        Assert.Contains("    L[\"Chinese\"] = \"中文测试\"", capturedLines);
        Assert.Contains("    L[\"Japanese\"] = \"日本語テスト\"", capturedLines);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_PreservesIndentation()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";
        var filePath = "/addon/Localization/enUS.lua";
        var existingLines = new[]
        {
            "if locale == \"enUS\" then",
            "    local L = TRB.Localization",
            "    L[\"Key1\"] = \"Value1\"",
            "end"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(filePath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(filePath)).ReturnsAsync(existingLines);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        var translations = new Dictionary<string, string>
        {
            ["Key1"] = "Updated Value",
            ["Key2"] = "New Value"
        };

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        Assert.All(capturedLines.Where(l => l.Contains("L[")), 
            line => Assert.True(line.StartsWith("    "), $"Line should start with 4 spaces: {line}"));
    }

    [Fact]
    public async Task SaveLocaleFileAsync_PreservesExistingEscapeSequences()
    {
        // This test simulates the cleanup flow where values come from parsing
        // The parser extracts values with escape sequences as literal characters
        var localizationDir = "/addon/Localization";
        var localeCode = "frFR";
        var filePath = "/addon/Localization/frFR.lua";
        
        // Simulate what the parser extracts - literal backslash-n (two characters)
        // In C#, "\\n" is a string containing backslash followed by 'n'
        var translations = new Dictionary<string, string>
        {
            ["TestKey"] = "Line1\\nLine2\\n\\n"  // This is what parser extracts: \n as two chars
        };

        var existingLines = new[]
        {
            "local _, TRB = ...",
            "",
            "local locale = GetLocale()",
            "",
            "if locale == \"frFR\" then",
            "    local L = TRB.Localization",
            "    L[\"TestKey\"] = \"Line1\\nLine2\\n\\n\"",
            "end"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(filePath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(filePath)).ReturnsAsync(existingLines);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        // The output should preserve the original escape sequences, not double-escape them
        var testKeyLine = capturedLines.FirstOrDefault(l => l.Contains("TestKey"));
        Assert.NotNull(testKeyLine);
        Assert.Contains("Line1\\nLine2\\n\\n", testKeyLine);
        Assert.DoesNotContain("\\\\n", testKeyLine); // Should NOT be double-escaped
    }

    [Fact]
    public async Task SaveLocaleFileAsync_FileWithoutLocaleBlock_HandlesCorrectly()
    {
        // enUS.lua often doesn't have an "if locale ==" block - it's the base file
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";
        
        // Typical enUS structure without locale check
        var existingLines = new[]
        {
            "local _, TRB = ...",
            "",
            "TRB.Localization = {}",
            "local L = TRB.Localization",
            "",
            "L[\"ExistingKey\"] = \"Existing Value\"",
            "L[\"OrphanedKey\"] = \"This should be removed\"",
            "L[\"AnotherKey\"] = \"Another Value\"",
            ""
        };

        // Translations without the orphaned key
        var translations = new Dictionary<string, string>
        {
            ["ExistingKey"] = "Existing Value",
            ["AnotherKey"] = "Another Value",
            ["NewKey"] = "New Value"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(It.IsAny<string>())).ReturnsAsync(existingLines);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        
        // Should preserve header
        Assert.Contains("local _, TRB = ...", capturedLines);
        Assert.Contains("TRB.Localization = {}", capturedLines);
        
        // Should keep existing keys that are in translations
        Assert.Contains(capturedLines, l => l.Contains("ExistingKey"));
        Assert.Contains(capturedLines, l => l.Contains("AnotherKey"));
        
        // Should add new key
        Assert.Contains(capturedLines, l => l.Contains("NewKey"));
        
        // Should NOT contain orphaned key
        Assert.DoesNotContain(capturedLines, l => l.Contains("OrphanedKey"));
        
        // Should NOT have warning message
        Assert.DoesNotContain(capturedLines, l => l.Contains("Warning"));
    }

    [Fact]
    public async Task SaveLocaleFileAsync_FileWithoutLocaleBlock_RemovesOrphanedEntries()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";
        
        var existingLines = new[]
        {
            "local _, TRB = ...",
            "local L = TRB.Localization",
            "L[\"Keep1\"] = \"Value1\"",
            "L[\"Remove1\"] = \"Orphaned1\"",
            "L[\"Keep2\"] = \"Value2\"",
            "L[\"Remove2\"] = \"Orphaned2\"",
            "L[\"Keep3\"] = \"Value3\""
        };

        // Only include some keys - others should be removed
        var translations = new Dictionary<string, string>
        {
            ["Keep1"] = "Value1",
            ["Keep2"] = "Value2",
            ["Keep3"] = "Value3"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(It.IsAny<string>())).ReturnsAsync(existingLines);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        
        // Should keep the keys that are in translations
        Assert.Contains(capturedLines, l => l.Contains("Keep1"));
        Assert.Contains(capturedLines, l => l.Contains("Keep2"));
        Assert.Contains(capturedLines, l => l.Contains("Keep3"));
        
        // Should NOT contain orphaned keys
        Assert.DoesNotContain(capturedLines, l => l.Contains("Remove1"));
        Assert.DoesNotContain(capturedLines, l => l.Contains("Remove2"));
        
        // Count of L["key"] lines should be exactly 3
        var assignmentLines = capturedLines.Count(l => l.TrimStart().StartsWith("L[\""));
        Assert.Equal(3, assignmentLines);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_FileWithDuplicates_RemovesDuplicateEntries()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "enUS";
        
        // File with duplicate entries - same key appears multiple times
        var existingLines = new[]
        {
            "local _, TRB = ...",
            "local L = TRB.Localization",
            "L[\"DuplicateKey\"] = \"First Value\"",
            "L[\"UniqueKey\"] = \"Unique Value\"",
            "L[\"DuplicateKey\"] = \"Second Value\"",
            "L[\"AnotherKey\"] = \"Another Value\"",
            "L[\"DuplicateKey\"] = \"Third Value\""
        };

        // Translations with de-duplicated values (last value wins during parsing)
        var translations = new Dictionary<string, string>
        {
            ["DuplicateKey"] = "Third Value",
            ["UniqueKey"] = "Unique Value",
            ["AnotherKey"] = "Another Value"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(It.IsAny<string>())).ReturnsAsync(existingLines);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        
        // Count occurrences of DuplicateKey - should be exactly 1
        var duplicateKeyLines = capturedLines.Count(l => l.Contains("DuplicateKey"));
        Assert.Equal(1, duplicateKeyLines);
        
        // The value should be the last one (Third Value)
        Assert.Contains(capturedLines, l => l.Contains("DuplicateKey") && l.Contains("Third Value"));
        
        // Other keys should still be present
        Assert.Contains(capturedLines, l => l.Contains("UniqueKey"));
        Assert.Contains(capturedLines, l => l.Contains("AnotherKey"));
        
        // Total assignment lines should be 3
        var assignmentLines = capturedLines.Count(l => l.TrimStart().StartsWith("L[\""));
        Assert.Equal(3, assignmentLines);
    }

    [Fact]
    public async Task SaveLocaleFileAsync_FileWithDuplicatesInLocaleBlock_RemovesDuplicateEntries()
    {
        var localizationDir = "/addon/Localization";
        var localeCode = "deDE";
        var filePath = "/addon/Localization/deDE.lua";
        
        // File with locale block and duplicate entries
        var existingLines = new[]
        {
            "local _, TRB = ...",
            "",
            "local locale = GetLocale()",
            "",
            "if locale == \"deDE\" then",
            "    local L = TRB.Localization",
            "    L[\"DuplicateKey\"] = \"Erster Wert\"",
            "    L[\"UniqueKey\"] = \"Einzigartiger Wert\"",
            "    L[\"DuplicateKey\"] = \"Zweiter Wert\"",
            "end"
        };

        // Translations with de-duplicated values
        var translations = new Dictionary<string, string>
        {
            ["DuplicateKey"] = "Zweiter Wert",
            ["UniqueKey"] = "Einzigartiger Wert"
        };

        _fileSystemMock.Setup(fs => fs.DirectoryExists(localizationDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.FileExists(filePath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.ReadAllLinesAsync(filePath)).ReturnsAsync(existingLines);

        List<string>? capturedLines = null;
        _fileSystemMock.Setup(fs => fs.WriteAllLinesAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Callback<string, IEnumerable<string>>((path, lines) => capturedLines = lines.ToList())
            .Returns(Task.CompletedTask);

        await _writer.SaveLocaleFileAsync(localizationDir, localeCode, translations, createBackup: false);

        Assert.NotNull(capturedLines);
        
        // Count occurrences of DuplicateKey - should be exactly 1
        var duplicateKeyLines = capturedLines.Count(l => l.Contains("DuplicateKey"));
        Assert.Equal(1, duplicateKeyLines);
        
        // The value should be the last one (Zweiter Wert)
        Assert.Contains(capturedLines, l => l.Contains("DuplicateKey") && l.Contains("Zweiter Wert"));
        
        // Structure should be preserved
        Assert.Contains("if locale == \"deDE\" then", capturedLines);
        Assert.Contains("end", capturedLines);
        
        // Total assignment lines should be 2
        var assignmentLines = capturedLines.Count(l => l.TrimStart().StartsWith("L[\""));
        Assert.Equal(2, assignmentLines);
    }

    #endregion
}
