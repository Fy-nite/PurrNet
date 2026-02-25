using Microsoft.Extensions.Logging;
using PurrLauncher.Pages;
using PurrLauncher.Services;
using PurrLauncher.ViewModels;

namespace PurrLauncher
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // ── Services ──────────────────────────────────────────────────────
            builder.Services.AddSingleton<PurrApiService>();
            builder.Services.AddSingleton<PackageInstallService>();

            // ── ViewModels ────────────────────────────────────────────────────
            builder.Services.AddSingleton<BrowseViewModel>();
            builder.Services.AddSingleton<InstalledViewModel>();
            builder.Services.AddTransient<PackageDetailViewModel>();

            // ── Pages ─────────────────────────────────────────────────────────
            builder.Services.AddSingleton<BrowsePage>();
            builder.Services.AddSingleton<InstalledPage>();
            builder.Services.AddTransient<PackageDetailPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
