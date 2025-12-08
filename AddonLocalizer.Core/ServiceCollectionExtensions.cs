using AddonLocalizer.Core.Interfaces;
using AddonLocalizer.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AddonLocalizer.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all AddonLocalizer.Core services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddAddonLocalizerCore(this IServiceCollection services)
    {
        // Register file system service
        services.AddSingleton<IFileSystemService, FileSystemService>();

        // Register Lua localization parser service
        services.AddTransient<ILuaLocalizationParserService, LuaLocalizationParserService>();

        return services;
    }
}