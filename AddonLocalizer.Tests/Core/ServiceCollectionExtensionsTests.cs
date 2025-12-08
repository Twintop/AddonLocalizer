using Microsoft.Extensions.DependencyInjection;
using AddonLocalizer.Core;

namespace AddonLocalizer.Tests.Core;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAddonLocalizerCore_RegistersFileSystemService()
    {
        var services = new ServiceCollection();

        services.AddAddonLocalizerCore();

        var serviceProvider = services.BuildServiceProvider();
        var fileSystemService = serviceProvider.GetService<IFileSystemService>();

        Assert.NotNull(fileSystemService);
        Assert.IsType<FileSystemService>(fileSystemService);
    }

    [Fact]
    public void AddAddonLocalizerCore_RegistersLuaLocalizationParserService()
    {
        var services = new ServiceCollection();

        services.AddAddonLocalizerCore();

        var serviceProvider = services.BuildServiceProvider();
        var parser = serviceProvider.GetService<ILuaLocalizationParserService>();

        Assert.NotNull(parser);
        Assert.IsType<LuaLocalizationParserService>(parser);
    }

    [Fact]
    public void AddAddonLocalizerCore_FileSystemServiceIsSingleton()
    {
        var services = new ServiceCollection();

        services.AddAddonLocalizerCore();

        var serviceProvider = services.BuildServiceProvider();
        var instance1 = serviceProvider.GetService<IFileSystemService>();
        var instance2 = serviceProvider.GetService<IFileSystemService>();

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void AddAddonLocalizerCore_LuaLocalizationParserServiceIsTransient()
    {
        var services = new ServiceCollection();

        services.AddAddonLocalizerCore();

        var serviceProvider = services.BuildServiceProvider();
        var instance1 = serviceProvider.GetService<ILuaLocalizationParserService>();
        var instance2 = serviceProvider.GetService<ILuaLocalizationParserService>();

        Assert.NotSame(instance1, instance2);
    }

    [Fact]
    public void AddAddonLocalizerCore_ParserReceivesFileSystemServiceViaDI()
    {
        var services = new ServiceCollection();

        services.AddAddonLocalizerCore();

        var serviceProvider = services.BuildServiceProvider();
        var parser = serviceProvider.GetService<ILuaLocalizationParserService>();

        Assert.NotNull(parser);
    }

    [Fact]
    public void AddAddonLocalizerCore_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddAddonLocalizerCore();

        Assert.Same(services, result);
    }
}