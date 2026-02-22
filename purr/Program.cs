using System.CommandLine;
using System.Text.Json;
using Fur.Services;
using Fur.Models;

namespace Fur
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Load fursettings.json for repository URLs
            var configPath = Path.Combine(AppContext.BaseDirectory, "fursettings.json");
            FurSettings settings;
            if (File.Exists(configPath))
            {
                var json = await File.ReadAllTextAsync(configPath);
                settings = JsonSerializer.Deserialize<FurSettings>(json) ?? new FurSettings();
            }
            else
            {
                settings = new FurSettings();
            }

            var rootCommand = new RootCommand("FUR - Finite User Repository Package Manager");

            // Install command
            var installCommand = new Command("install", "Install a package");
            var packageArg = new Argument<string>("package", "Package name with optional version (name@version)");
            installCommand.AddArgument(packageArg);
            installCommand.SetHandler(async (string package) =>
            {
                var packageManager = new PackageManager(settings.RepositoryUrls);
                await packageManager.InstallPackageAsync(package);
            }, packageArg);

            // Search command
            var searchCommand = new Command("search", "Search for packages");
            var queryArg = new Argument<string>("query", "Search query");
            searchCommand.AddArgument(queryArg);
            searchCommand.SetHandler(async (string query) =>
            {
                var packageManager = new PackageManager(settings.RepositoryUrls);
                await packageManager.SearchPackagesAsync(query);
            }, queryArg);

            // List command
            var listCommand = new Command("list", "List all packages or filter by category");
            var sortOption = new Option<string>("--sort", "Sort method (mostDownloads, recentlyUpdated, etc.)");
            var categoryOption = new Option<string>("--category", "Filter packages by category name");
            listCommand.AddOption(sortOption);
            listCommand.AddOption(categoryOption);
            listCommand.SetHandler(async (string sort, string category) =>
            {
                var packageManager = new PackageManager(settings.RepositoryUrls);
                if (!string.IsNullOrWhiteSpace(category))
                {
                    await packageManager.ListPackagesByCategoryAsync(category);
                }
                else
                {
                    await packageManager.ListPackagesAsync(sort);
                }
            }, sortOption, categoryOption);

            // Info command
            var infoCommand = new Command("info", "Get package information");
            var infoPackageArg = new Argument<string>("package", "Package name");
            var versionOption = new Option<string>("--version", "Specific version");
            infoCommand.AddArgument(infoPackageArg);
            infoCommand.AddOption(versionOption);
            infoCommand.SetHandler(async (string package, string version) =>
            {
                var packageManager = new PackageManager(settings.RepositoryUrls);
                await packageManager.GetPackageInfoAsync(package, version);
            }, infoPackageArg, versionOption);

            // Stats command
            var statsCommand = new Command("stats", "Show repository statistics");
            statsCommand.SetHandler(async () =>
            {
                var packageManager = new PackageManager(settings.RepositoryUrls);
                await packageManager.ShowStatisticsAsync();
            });

            // Update command
            var updateCommand = new Command("update", "Update an installed package");
            var updatePackageArg = new Argument<string>("package", "Package name to update");
            updateCommand.AddArgument(updatePackageArg);
            updateCommand.SetHandler(async (string package) =>
            {
                var packageManager = new PackageManager();
                await packageManager.UpdatePackageAsync(package);
            }, updatePackageArg);

            // Upgrade command
            var upgradeCommand = new Command("upgrade", "Upgrade a package to a specific version");
            var upgradeArg = new Argument<string>("package", "Package name with optional version (name@version)");
            upgradeCommand.AddArgument(upgradeArg);
            upgradeCommand.SetHandler(async (string package) =>
            {
                var packageManager = new PackageManager(settings.RepositoryUrls);
                await packageManager.UpgradePackageAsync(package);
            }, upgradeArg);

            // Downgrade command
            var downgradeCommand = new Command("downgrade", "Downgrade a package to a specific version");
            var downgradeArg = new Argument<string>("package", "Package name with version (name@version)");
            downgradeCommand.AddArgument(downgradeArg);
            downgradeCommand.SetHandler(async (string package) =>
            {
                var packageManager = new PackageManager(settings.RepositoryUrls);
                await packageManager.DowngradePackageAsync(package);
            }, downgradeArg);

            // Versions command
            var versionsCommand = new Command("versions", "List available versions of a package");
            var versionsArg = new Argument<string>("package", "Package name");
            versionsCommand.AddArgument(versionsArg);
            versionsCommand.SetHandler(async (string package) =>
            {
                var packageManager = new PackageManager(settings.RepositoryUrls);
                await packageManager.ListVersionsAsync(package);
            }, versionsArg);

            // Uninstall command
            var uninstallCommand = new Command("uninstall", "Uninstall a package");
            var uninstallArg = new Argument<string>("package", "Package name");
            uninstallCommand.AddArgument(uninstallArg);
            uninstallCommand.SetHandler(async (string package) =>
            {
                var packageManager = new PackageManager(settings.RepositoryUrls);
                await packageManager.UninstallPackageAsync(package);
            }, uninstallArg);

            rootCommand.AddCommand(installCommand);
            rootCommand.AddCommand(searchCommand);
            rootCommand.AddCommand(listCommand);
            rootCommand.AddCommand(infoCommand);
            rootCommand.AddCommand(statsCommand);
            rootCommand.AddCommand(upgradeCommand);
            rootCommand.AddCommand(downgradeCommand);
            rootCommand.AddCommand(versionsCommand);
            rootCommand.AddCommand(uninstallCommand);

            return await rootCommand.InvokeAsync(args);
        }
    }
}
