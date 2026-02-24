using System.Diagnostics;
using System.Text.Json;
using System.Net.Http;
using System.IO.Compression;
using Fur.Models;
using Fur.Utils;

namespace Fur.Services;

public class PackageManager
{
    private readonly ApiService _apiService;
    private readonly string _packagesDirectory;

    public PackageManager(string[]? repositoryUrls = null)
    {
        _apiService = repositoryUrls != null
            ? new ApiService(repositoryUrls)
            : new ApiService();
        _packagesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fur", "packages");
        Directory.CreateDirectory(_packagesDirectory);
    }

    public async Task InstallPackageAsync(string packageSpec)
    {
        var (packageName, version) = ParsePackageSpec(packageSpec);
        
        ConsoleHelper.WriteStep("Installing", packageName + (version != null ? $"@{version}" : ""));
        
        var packageInfo = await _apiService.GetPackageInfoAsync(packageName, version);
        if (packageInfo == null)
        {
            ConsoleHelper.WriteError($"Package '{packageName}' not found");
            return;
        }

        // Install dependencies first
        foreach (var dependency in packageInfo.Dependencies)
        {
            ConsoleHelper.WriteStep("Dependency", dependency);
            await InstallPackageAsync(dependency);
        }

        // Download and install the package
        await DownloadAndInstallPackageAsync(packageInfo);
        
        // Track download
        await _apiService.TrackDownloadAsync(packageName);
        
        ConsoleHelper.WriteSuccess($"Installed ");
        ConsoleHelper.WritePackage(packageName, packageInfo.Version);
        Console.WriteLine();
    }

    private async Task DownloadAndInstallPackageAsync(FurConfig packageInfo)
    {
        // If an installer script is provided in the package metadata, keep old behaviour (clone + run script).
        // Otherwise, try to download the package's release assets and install any suitable binary onto the user's PATH.
        if (!string.IsNullOrEmpty(packageInfo.Installer))
        {
            var packageDir = Path.Combine(_packagesDirectory, packageInfo.Name);
            if (Directory.Exists(packageDir) && Directory.Exists(Path.Combine(packageDir, ".git")))
            {
                ConsoleHelper.WriteStep("Updating", $"{packageInfo.Name} to v{packageInfo.Version}");
                await UpdateExistingPackageAsync(packageDir, packageInfo);
            }
            else
            {
                ConsoleHelper.WriteStep("Cloning", packageInfo.Git);
                Directory.CreateDirectory(packageDir);
                await CloneNewPackageAsync(packageDir, packageInfo);
            }

            // Run the installer script
            var installerPath = Path.Combine(packageDir, packageInfo.Installer);
            if (File.Exists(installerPath))
            {
                ConsoleHelper.WriteStep("Running", packageInfo.Installer);
                try
                {
                    await RunInstallerScript(installerPath, showOutput: true);
                    ConsoleHelper.WriteSuccess("Installer completed successfully");
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteError($"Installer failed: {ex.Message}");
                    throw; // Re-throw to prevent marking package as successfully installed
                }
            }
            else
            {
                ConsoleHelper.WriteWarning($"Installer script '{packageInfo.Installer}' not found, skipping");
            }

            // Save package metadata
            var metadataPath = Path.Combine(packageDir, "furconfig.json");
            var json = JsonSerializer.Serialize(packageInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metadataPath, json);
        }
        else
        {
            // Try to download release assets and install
            try
            {
                await DownloadReleaseAssetAndInstallAsync(packageInfo);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteWarning($"Could not install from release assets: {ex.Message}");
                ConsoleHelper.WriteInfo("Falling back to repository clone and attempting to run installer if present.");

                // Fallback: clone repository to preserve previous behaviour
                var packageDir = Path.Combine(_packagesDirectory, packageInfo.Name);
                Directory.CreateDirectory(packageDir);
                await CloneNewPackageAsync(packageDir, packageInfo);
            }
        }
    }

    private async Task DownloadReleaseAssetAndInstallAsync(FurConfig packageInfo)
    {
        // Parse GitHub owner/repo from the Git URL
        if (string.IsNullOrEmpty(packageInfo.Git))
            throw new Exception("No repository specified");

        Uri gitUri;
        try { gitUri = new Uri(packageInfo.Git); } catch { throw new Exception("Invalid Git URL"); }

        // Only support github.com hosts for release downloads
        if (!gitUri.Host.Contains("github.com"))
            throw new Exception("Release asset download currently only supports GitHub repositories");

        var segments = gitUri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2)
            throw new Exception("Could not parse owner/repo from Git URL");

        var owner = segments[0];
        var repo = segments[1].EndsWith(".git") ? segments[1][..^4] : segments[1];

        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("purr-cli/1.0");
        http.Timeout = TimeSpan.FromSeconds(30);

        string apiUrl = string.IsNullOrEmpty(packageInfo.Version) || packageInfo.Version == "latest"
            ? $"https://api.github.com/repos/{owner}/{repo}/releases/latest"
            : $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{packageInfo.Version}";

        var resp = await http.GetAsync(apiUrl);
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"GitHub API returned {resp.StatusCode}");

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.GetArrayLength() == 0)
            throw new Exception("No release assets found");

        // Choose an asset that appears to match the current OS
        string? chosenUrl = null;
        string? chosenName = null;
        var os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "mac" : "linux";

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            var url = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
            var lname = name.ToLowerInvariant();
            if (lname.Contains(os) || lname.Contains("win") || lname.Contains("darwin") || lname.Contains("linux") )
            {
                chosenUrl = url; chosenName = name; break;
            }
        }

        // fallback to first asset
        if (chosenUrl == null)
        {
            var first = assets[0];
            chosenName = first.GetProperty("name").GetString();
            chosenUrl = first.GetProperty("browser_download_url").GetString();
        }

        if (chosenUrl == null)
            throw new Exception("No suitable release asset found");

        ConsoleHelper.WriteStep("Downloading asset", chosenName ?? "(asset)");
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        var localPath = Path.Combine(tmp, chosenName ?? "asset.bin");

        using (var s = await http.GetStreamAsync(chosenUrl))
        using (var fs = File.Create(localPath))
            await s.CopyToAsync(fs);

        // Prepare user's bin directory
        var userBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".purr", "bin");
        Directory.CreateDirectory(userBin);

        // If it's a zip, try to extract and find an executable
            if (localPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var extractDir = Path.Combine(tmp, "ex");
            ZipFile.ExtractToDirectory(localPath, extractDir);
            // find candidate executables
            var candidates = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories)
                .Where(f => (OperatingSystem.IsWindows() && f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) || (!OperatingSystem.IsWindows() && Path.GetExtension(f) == string.Empty))
                .ToList();
            if (candidates.Count == 0)
                throw new Exception("No executable found inside zip asset");

            var exe = candidates[0];
            var dest = Path.Combine(userBin, Path.GetFileName(exe));
            File.Copy(exe, dest, true);
            if (!OperatingSystem.IsWindows())
                await RunCommandAsync("chmod", $"+x \"{dest}\"");

            // create simple shim name (package name)
            var shim = Path.Combine(userBin, packageInfo.Name + (OperatingSystem.IsWindows() ? ".exe" : ""));
            try { if (File.Exists(shim)) File.Delete(shim); } catch {}
            File.Copy(dest, shim);

            ConsoleHelper.WriteSuccess($"Installed {Path.GetFileName(dest)} to {userBin}");
            PrintPathInstructions(userBin, packageInfo.Name);
            return;
        }

        // Otherwise treat the asset as a single executable binary
        var finalName = Path.GetFileName(localPath);
        var target = Path.Combine(userBin, finalName);
        File.Move(localPath, target);
        if (!OperatingSystem.IsWindows())
            await RunCommandAsync("chmod", $"+x \"{target}\"");

        // create shim with package name
        var shimPath = Path.Combine(userBin, packageInfo.Name + (OperatingSystem.IsWindows() ? ".exe" : ""));
        try { if (File.Exists(shimPath)) File.Delete(shimPath); } catch {}
        File.Copy(target, shimPath);

        ConsoleHelper.WriteSuccess($"Installed {finalName} to {userBin}");
        PrintPathInstructions(userBin, packageInfo.Name);
    }

    private void PrintPathInstructions(string userBin, string packageName)
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var parts = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
            if (parts.Any(p => string.Equals(Path.GetFullPath(p), Path.GetFullPath(userBin), StringComparison.OrdinalIgnoreCase)))
            {
                // already on PATH
                ConsoleHelper.WriteInfo($"{userBin} is already in PATH. You can run '{packageName}' now.");
                return;
            }

            ConsoleHelper.WriteWarning($"{userBin} is not currently in your PATH.");

            if (OperatingSystem.IsWindows())
            {
                ConsoleHelper.WriteInfo("PowerShell (current session):");
                ConsoleHelper.WriteDim($"  $env:Path = \"$env:USERPROFILE\\.purr\\bin;$env:Path\"");
                ConsoleHelper.WriteInfo("PowerShell (persist):");
                ConsoleHelper.WriteDim($"  setx PATH \"%USERPROFILE%\\.purr\\bin;%PATH%\"");
                ConsoleHelper.WriteInfo("Command Prompt (persist):");
                ConsoleHelper.WriteDim($"  setx PATH \"%USERPROFILE%\\.purr\\bin;%PATH%\"");
            }
            else
            {
                var shell = Environment.GetEnvironmentVariable("SHELL") ?? string.Empty;
                string rcFile = "~/.profile";
                if (shell.Contains("zsh")) rcFile = "~/.zshrc";
                else if (shell.Contains("bash")) rcFile = "~/.bashrc";

                ConsoleHelper.WriteInfo("Add to current session:");
                ConsoleHelper.WriteDim($"  export PATH=\"$HOME/.purr/bin:$PATH\"");

                ConsoleHelper.WriteInfo($"Persist for future sessions (append to {rcFile}):");
                ConsoleHelper.WriteDim($"  echo 'export PATH=\"$HOME/.purr/bin:$PATH\"' >> {rcFile}");

                ConsoleHelper.WriteInfo("Fish shell (persistent):");
                ConsoleHelper.WriteDim($"  set -U fish_user_paths $HOME/.purr/bin $fish_user_paths");
            }

            Console.WriteLine();
            ConsoleHelper.WriteInfo($"After adding, open a new shell or source your rc file to run '{packageName}' immediately.");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteWarning($"Could not determine PATH instructions: {ex.Message}");
        }
    }

    private async Task UpdateExistingPackageAsync(string packageDir, FurConfig packageInfo)
    {
        try
        {
            // Fetch latest changes
            await RunCommandAsync("git", $"-C \"{packageDir}\" fetch --all --tags");
            
            // Try to switch to the specific version (could be tag or branch)
            try
            {
                await RunCommandAsync("git", $"-C \"{packageDir}\" checkout {packageInfo.Version}");
                ConsoleHelper.WriteStep("Switched", $"to version {packageInfo.Version}");
            }
            catch
            {
                // If checkout fails, try with origin/ prefix for remote branches
                try
                {
                    await RunCommandAsync("git", $"-C \"{packageDir}\" checkout origin/{packageInfo.Version}");
                    ConsoleHelper.WriteStep("Switched", $"to remote branch origin/{packageInfo.Version}");
                }
                catch
                {
                    ConsoleHelper.WriteWarning($"Could not find version {packageInfo.Version}, staying on current branch");
                }
            }
            
            // Pull latest changes if we're on a branch (not a specific tag)
            try
            {
                await RunCommandAsync("git", $"-C \"{packageDir}\" pull");
            }
            catch
            {
                // Ignore pull errors (might be on a detached HEAD for tags)
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteWarning($"Failed to update existing package: {ex.Message}");
            ConsoleHelper.WriteInfo("Proceeding with current local version...");
        }
    }

    private async Task CloneNewPackageAsync(string packageDir, FurConfig packageInfo)
    {
        // Clone the git repository
        await RunCommandAsync("git", $"clone {packageInfo.Git} \"{packageDir}\"");

        // Switch to specific version if not the default
        if (!string.IsNullOrEmpty(packageInfo.Version) && packageInfo.Version != "latest")
        {
            try
            {
                await RunCommandAsync("git", $"-C \"{packageDir}\" checkout {packageInfo.Version}");
                ConsoleHelper.WriteStep("Switched", $"to version {packageInfo.Version}");
            }
            catch
            {
                ConsoleHelper.WriteWarning($"Could not find version {packageInfo.Version}, using default branch");
            }
        }
    }

    private async Task RunInstallerScript(string scriptPath)
    {
        await RunInstallerScript(scriptPath, showOutput: false);
    }

    private async Task RunInstallerScript(string scriptPath, bool showOutput)
    {
        var extension = Path.GetExtension(scriptPath).ToLowerInvariant();
        var (command, arguments) = GetShellForScript(extension, scriptPath);

        if (command == null)
        {
            ConsoleHelper.WriteError($"Unsupported script type: {extension}");
            throw new Exception($"Cannot execute installer with unsupported extension: {extension}");
        }

        // Determine install directory and package name for env vars
        string? installDir = Path.GetDirectoryName(scriptPath);
        string? packageName = installDir != null ? Path.GetFileName(installDir) : null;
        string cwd = Directory.GetCurrentDirectory();

        await RunCommandAsync(command, arguments, showOutput, new Dictionary<string, string?> {
            {"PURR_CWD", cwd},
            {"PURR_INSTALL_DIR", installDir},
            {"PURR_PACKAGE_NAME", packageName}
        });
    }

    private static (string? command, string arguments) GetShellForScript(string extension, string scriptPath)
    {
        return extension switch
        {
            ".sh" => ("bash", scriptPath),
            ".ps1" => ("pwsh", $"-ExecutionPolicy Bypass -File \"{scriptPath}\""),
            ".py" => ("python", scriptPath),
            ".js" => ("node", scriptPath),
            ".rb" => ("ruby", scriptPath),
            ".cmd" or ".bat" => (OperatingSystem.IsWindows() ? "cmd" : null, 
                                OperatingSystem.IsWindows() ? $"/c \"{scriptPath}\"" : ""),
            ".exe" => (OperatingSystem.IsWindows() ? scriptPath : null, ""),
            "" => DetermineShellForExtensionlessScript(scriptPath),
            _ => (null, "")
        };
    }

    private static (string? command, string arguments) DetermineShellForExtensionlessScript(string scriptPath)
    {
        // For extensionless scripts, try to read the shebang line
        try
        {
            var firstLine = File.ReadLines(scriptPath).FirstOrDefault();
            if (firstLine?.StartsWith("#!") == true)
            {
                var shebang = firstLine[2..].Trim();
                
                // Common shebang patterns
                if (shebang.Contains("bash") || shebang.Contains("sh"))
                    return ("bash", scriptPath);
                if (shebang.Contains("python"))
                    return ("python", scriptPath);
                if (shebang.Contains("node"))
                    return ("node", scriptPath);
                if (shebang.Contains("ruby"))
                    return ("ruby", scriptPath);
                
                // Use the shebang directly if it's an absolute path
                if (shebang.StartsWith("/") && File.Exists(shebang))
                    return (shebang, scriptPath);
            }
        }
        catch
        {
            // Ignore errors reading the file
        }

        // Default to bash on Unix-like systems, or make executable and run directly
        if (OperatingSystem.IsWindows())
            return (null, "");
        
        // Make the script executable and run it directly
        try
        {
            RunCommandAsync("chmod", $"+x \"{scriptPath}\"").Wait();
            return (scriptPath, "");
        }
        catch
        {
            return ("bash", scriptPath); // Fallback to bash
        }
    }

    public async Task SearchPackagesAsync(string query)
    {
        ConsoleHelper.WriteStep("Searching", $"'{query}'");
        
        var results = await _apiService.SearchPackagesAsync(query);
        if (results == null || (results.Packages.Length == 0 && results.DetailedPackages.Length == 0))
        {
            ConsoleHelper.WriteWarning("No packages found");
            return;
        }

        // If we have detailed packages, show them with full information
        if (results.DetailedPackages.Length > 0)
        {
            ConsoleHelper.WriteHeader($"Found {results.PackageCount} packages");
            foreach (var package in results.DetailedPackages)
            {
                Console.WriteLine();
                Console.Write("📦 ");
                ConsoleHelper.WritePackage(package.Name, package.Version);
                Console.WriteLine();
                
                if (!string.IsNullOrEmpty(package.Description))
                {
                    ConsoleHelper.WriteDim("   ");
                    Console.WriteLine(package.Description);
                }
                if (package.Authors.Length > 0)
                {
                    ConsoleHelper.WriteDim("   Authors: ");
                    Console.WriteLine(string.Join(", ", package.Authors));
                }
                if (package.Dependencies.Length > 0)
                {
                    ConsoleHelper.WriteDim("   Dependencies: ");
                    Console.WriteLine(string.Join(", ", package.Dependencies));
                }
                if (!string.IsNullOrEmpty(package.Homepage))
                {
                    ConsoleHelper.WriteDim("   Homepage: ");
                    Console.WriteLine(package.Homepage);
                }
            }
        }
        // Fallback to simple package names if detailed info isn't available
        else
        {
            ConsoleHelper.WriteHeader($"Found {results.PackageCount} packages");
            foreach (var package in results.Packages)
            {
                Console.Write("  • ");
                ConsoleHelper.WritePackage(package);
                Console.WriteLine();
            }
        }
    }

    public async Task ListCategoriesAsync()
    {
        ConsoleHelper.WriteStep("Fetching", "categories");

        var categories = await _apiService.GetCategoriesAsync();
        if (categories == null || categories.Length == 0)
        {
            ConsoleHelper.WriteWarning("No categories available");
            return;
        }

        ConsoleHelper.WriteHeader("Categories");
        foreach (var c in categories)
        {
            Console.WriteLine($"  • {c}");
        }
    }

    public async Task ListPackagesByCategoryAsync(string category)
    {
        ConsoleHelper.WriteStep("Fetching", $"packages in category '{category}'");

        var results = await _apiService.GetPackagesByCategoryAsync(category);
        if (results == null || results.Length == 0)
        {
            ConsoleHelper.WriteWarning("No packages found for this category");
            return;
        }

        ConsoleHelper.WriteHeader($"Packages in '{category}' ({results.Length})");
        foreach (var pkg in results)
        {
            Console.Write("  • ");
            ConsoleHelper.WritePackage(pkg.Name, pkg.Version);
            Console.WriteLine();
            if (!string.IsNullOrEmpty(pkg.Description))
            {
                ConsoleHelper.WriteDim("   ");
                Console.WriteLine(pkg.Description);
            }
        }
    }

    public async Task ListPackagesAsync(string? sort = null)
    {
        ConsoleHelper.WriteStep("Fetching", "package list");
        
        var results = await _apiService.GetPackagesAsync(sort);
        if (results == null || results.Packages.Length == 0)
        {
            ConsoleHelper.WriteWarning("No packages available");
            return;
        }

        ConsoleHelper.WriteHeader($"Available packages ({results.PackageCount} total)");
        foreach (var package in results.Packages)
        {
            Console.Write("  • ");
            ConsoleHelper.WritePackage(package);
            Console.WriteLine();
        }
    }

    public async Task ShowStatisticsAsync()
    {
        ConsoleHelper.WriteStep("Fetching", "repository statistics");
        
        var stats = await _apiService.GetStatisticsAsync();
        if (stats == null)
        {
            ConsoleHelper.WriteError("Could not retrieve statistics");
            return;
        }

        ConsoleHelper.WriteHeader("Repository Statistics");
        Console.WriteLine($"📊 Total Packages: {stats.TotalPackages}");
        Console.WriteLine($"✅ Active Packages: {stats.ActivePackages}");
        Console.WriteLine($"⬇️  Total Downloads: {stats.TotalDownloads:N0}");
        Console.WriteLine($"👁️  Total Views: {stats.TotalViews:N0}");
        
        if (stats.PopularAuthors.Length > 0)
        {
            Console.WriteLine($"👥 Popular Authors: {string.Join(", ", stats.PopularAuthors)}");
        }
        
        if (stats.MostDownloaded.Length > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("🔥 Most Downloaded:");
            Console.ResetColor();
            foreach (var package in stats.MostDownloaded.Take(5))
            {
                Console.Write("  • ");
                ConsoleHelper.WritePackage(package.Name);
                ConsoleHelper.WriteDim($" ({package.Downloads:N0} downloads)");
                Console.WriteLine();
            }
        }
        
        if (stats.RecentlyAdded.Length > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("🆕 Recently Added:");
            Console.ResetColor();
            foreach (var package in stats.RecentlyAdded.Take(5))
            {
                Console.Write("  • ");
                ConsoleHelper.WritePackage(package.Name, package.Version);
                Console.WriteLine();
            }
        }
        
        Console.WriteLine();
        ConsoleHelper.WriteDim($"Last Updated: {stats.LastUpdated:yyyy-MM-dd HH:mm}");
        Console.WriteLine();
    }

    public async Task GetPackageInfoAsync(string packageName, string? version = null)
    {
        ConsoleHelper.WriteStep("Getting", $"info for {packageName}");
        
        var packageInfo = await _apiService.GetPackageInfoAsync(packageName, version);
        if (packageInfo == null)
        {
            ConsoleHelper.WriteError($"Package '{packageName}' not found");
            return;
        }

        ConsoleHelper.WriteHeader("Package Information");
        Console.Write("📦 Name: ");
        ConsoleHelper.WritePackage(packageInfo.Name, packageInfo.Version);
        Console.WriteLine();
        Console.WriteLine($"👥 Authors: {string.Join(", ", packageInfo.Authors)}");
        Console.WriteLine($"🏠 Homepage: {packageInfo.Homepage}");
        Console.WriteLine($"🐛 Issue Tracker: {packageInfo.IssueTracker}");
        Console.WriteLine($"📂 Git: {packageInfo.Git}");
        Console.WriteLine($"⚙️  Installer: {packageInfo.Installer}");
        Console.WriteLine($"📋 Dependencies: {string.Join(", ", packageInfo.Dependencies)}");
    }

    public async Task UpgradePackageAsync(string packageSpec)
    {
        var (packageName, version) = ParsePackageSpec(packageSpec);
        ConsoleHelper.WriteStep("Upgrading", packageName + (version != null ? $"@{version}" : ""));
        await InstallPackageAsync(packageName + (version != null ? $"@{version}" : ""));
    }

    public async Task DowngradePackageAsync(string packageSpec)
    {
        var (packageName, version) = ParsePackageSpec(packageSpec);
        if (version == null)
        {
            ConsoleHelper.WriteWarning($"No version specified. Use 'purr versions {packageName}' to see available versions.");
            ConsoleHelper.WriteInfo($"Usage: purr downgrade {packageName}@<version>");
            return;
        }
        ConsoleHelper.WriteStep("Downgrading", $"{packageName} to v{version}");
        await InstallPackageAsync($"{packageName}@{version}");
    }

    public async Task ListVersionsAsync(string packageName)
    {
        ConsoleHelper.WriteStep("Fetching", $"versions for '{packageName}'");

        var versions = await _apiService.GetPackageVersionsAsync(packageName);
        if (versions == null || versions.Length == 0)
        {
            ConsoleHelper.WriteWarning($"No versions found for '{packageName}'");
            return;
        }

        ConsoleHelper.WriteHeader($"Available versions of {packageName}");
        for (int i = 0; i < versions.Length; i++)
        {
            var v = versions[i];
            Console.Write("  ");
            if (i == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("● ");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("○ ");
            }
            Console.ResetColor();
            ConsoleHelper.WritePackage(packageName, v);
            if (i == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("  (latest)");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        Console.WriteLine();
        ConsoleHelper.WriteInfo($"Install a specific version: purr install {packageName}@<version>");
        ConsoleHelper.WriteInfo($"Downgrade to a version:    purr downgrade {packageName}@<version>");
    }

    public async Task UninstallPackageAsync(string packageName)
    {
        var packageDir = Path.Combine(_packagesDirectory, packageName);
        if (!Directory.Exists(packageDir))
        {
            ConsoleHelper.WriteWarning($"Package '{packageName}' is not installed");
            return;
        }

        // Run uninstall script if present
        var configPath = Path.Combine(packageDir, "furconfig.json");
        if (File.Exists(configPath))
        {
            var configJson = await File.ReadAllTextAsync(configPath);
            var packageInfo = JsonSerializer.Deserialize<FurConfig>(configJson);
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.Installer))
            {
                var uninstallScript = packageInfo.Installer.Replace("install", "uninstall");
                var uninstallPath = Path.Combine(packageDir, uninstallScript);
                if (File.Exists(uninstallPath))
                {
                    ConsoleHelper.WriteStep("Running", uninstallScript);
                    try
                    {
                        await RunInstallerScript(uninstallPath, showOutput: true);
                        ConsoleHelper.WriteSuccess("Uninstaller completed successfully");
                    }
                    catch (Exception ex)
                    {
                        ConsoleHelper.WriteError($"Uninstaller failed: {ex.Message}");
                    }
                }
            }
        }

        // Remove package directory
        try
        {
            Directory.Delete(packageDir, true);
            ConsoleHelper.WriteSuccess($"Uninstalled {packageName}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Failed to uninstall: {ex.Message}");
        }
    }

    public async Task UpdatePackageAsync(string packageSpec)
    {
        var (packageName, version) = ParsePackageSpec(packageSpec);
        ConsoleHelper.WriteStep("Updating", packageName + (version != null ? $"@{version}" : ""));
        await InstallPackageAsync(packageName + (version != null ? $"@{version}" : ""));
    }

    private static (string name, string? version) ParsePackageSpec(string packageSpec)
    {
        var parts = packageSpec.Split('@');
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], null);
    }

    private static async Task RunCommandAsync(string command, string arguments, bool showOutput, Dictionary<string, string?>? extraEnv = null)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        // Inject environment variables if provided
        if (extraEnv != null)
        {
            foreach (var kv in extraEnv)
            {
                if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                    process.StartInfo.EnvironmentVariables[kv.Key] = kv.Value;
            }
        }

        process.Start();

        if (showOutput)
        {
            // Read output and error streams concurrently
            var outputTask = Task.Run(async () =>
            {
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = await process.StandardOutput.ReadLineAsync();
                    if (line != null)
                        Console.WriteLine(line);
                }
            });

            var errorTask = Task.Run(async () =>
            {
                while (!process.StandardError.EndOfStream)
                {
                    var line = await process.StandardError.ReadLineAsync();
                    if (line != null)
                        Console.Error.WriteLine(line);
                }
            });

            await Task.WhenAll(outputTask, errorTask);
        }

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = showOutput ? $"Process exited with code {process.ExitCode}" 
                                   : await process.StandardError.ReadToEndAsync();
            throw new Exception($"Command failed: {error}");
        }
    }

    private static async Task RunCommandAsync(string command, string arguments)
    {
        await RunCommandAsync(command, arguments, showOutput: false, null);
    }
}
