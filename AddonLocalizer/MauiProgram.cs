using AddonLocalizer.Core;
using AddonLocalizer.PageModels;
using AddonLocalizer.Pages;
using AddonLocalizer.Services;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;
using Syncfusion.Licensing;

namespace AddonLocalizer
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Register Syncfusion license from environment variable
            var syncfusionKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(syncfusionKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
            }

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionCore()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
    				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
    				{
    					handler.PlatformView.SingleSelectionFollowsFocus = false;
    				});
    				
    				// Force Entry and Editor text colors on Windows for better visibility in dark mode
    				Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("ForceDarkModeColors", (handler, view) =>
    				{
    					if (view.TextColor != null && handler.PlatformView != null)
    					{
    						var color = view.TextColor;
    						handler.PlatformView.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
    							Microsoft.UI.Colors.White);
    					}
    				});
    				
    				Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("ForceDarkModeColors", (handler, view) =>
    				{
    					if (view.TextColor != null && handler.PlatformView != null)
    					{
    						var color = view.TextColor;
    						handler.PlatformView.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
    							Microsoft.UI.Colors.White);
    					}
    				});
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            // Register Core services
            builder.Services.AddAddonLocalizerCore();

            // Register application services
            builder.Services.AddSingleton<IDialogService, DialogService>();

            // Register platform-specific services
#if WINDOWS
            builder.Services.AddSingleton<IFolderPickerService, Platforms.Windows.FolderPickerService>();
#elif ANDROID
            builder.Services.AddSingleton<IFolderPickerService, Platforms.Android.FolderPickerService>();
#elif IOS
            builder.Services.AddSingleton<IFolderPickerService, Platforms.iOS.FolderPickerService>();
#elif MACCATALYST
            builder.Services.AddSingleton<IFolderPickerService, Platforms.MacCatalyst.FolderPickerService>();
#endif

            // Register page models
            builder.Services.AddSingleton<LocalizationHomePageModel>();
            builder.Services.AddTransient<LocalizationGridPageModel>();
            builder.Services.AddTransient<LocalizationDetailPageModel>();

            // Register pages
            builder.Services.AddSingleton<LocalizationHomePage>();
            builder.Services.AddTransient<LocalizationGridPage>();
            builder.Services.AddTransient<LocalizationDetailPage>();

            return builder.Build();
        }
    }
}
