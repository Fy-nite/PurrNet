using System.Diagnostics;
using System.Text.Json;
using PurrLauncher.Models;

namespace PurrLauncher.Services;

public class PackageInstallService
{
    private static readonly string PackagesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".purr", "packages");

    // ─── Query ────────────────────────────────────────────────────────────────

    public bool IsPackageInstalled(string packageName)
    {
#if WINDOWS
        return Directory.Exists(Path.Combine(PackagesDir, packageName));
#else
        return false;
#endif
    }

    public IEnumerable<InstalledPackage> GetInstalledPackages()
    {
#if WINDOWS
        if (!Directory.Exists(PackagesDir))
            yield break;

        foreach (var dir in Directory.GetDirectories(PackagesDir))
        {
            var name = Path.GetFileName(dir);
            var version = ReadVersionFromDir(dir);

            yield return new InstalledPackage
            {
                Name = name,
                Version = version,
                InstallPath = dir,
                InstalledAt = Directory.GetCreationTime(dir)
            };
        }
#else
        yield break;
#endif
    }

    private static string ReadVersionFromDir(string dir)
    {
        // Try fursettings.json first, then furconfig.json
        foreach (var candidate in new[] { "fursettings.json", "furconfig.json" })
        {
            var path = Path.Combine(dir, candidate);
            if (!File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("version", out var v))
                    return v.GetString() ?? "unknown";
            }
            catch { /* ignore */ }
        }
        return "unknown";
    }

    // ─── Install ─────────────────────────────────────────────────────────────

    /// <summary>
    /// On Windows: runs <c>purr install &lt;name&gt;</c> as a subprocess.
    /// On Android: opens the package's GitHub releases page in the browser.
    /// </summary>
    public async Task<(bool Success, string Output)> InstallPackageAsync(
        string packageName,
        string? releasesUrl = null,
        IProgress<string>? progress = null)
    {
#if WINDOWS
        var purrExe = FindPurrExecutable();
        if (purrExe is null)
            return (false,
                "purr CLI not found in PATH.\n" +
                "Install it from: https://github.com/Fy-nite/purrnet");

        return await RunProcessAsync(purrExe, $"install {packageName}", progress);

#elif ANDROID
        // Open GitHub releases (or homepage) in the system browser
        var url = !string.IsNullOrEmpty(releasesUrl)
            ? releasesUrl
            : $"https://github.com/search?q={Uri.EscapeDataString(packageName)}+apk&type=repositories";

        await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        return (true, "Opened releases page in browser.");

#else
        return (false, "Installation is not supported on this platform.");
#endif
    }

    // ─── Uninstall ────────────────────────────────────────────────────────────

    public async Task<(bool Success, string Output)> UninstallPackageAsync(string packageName)
    {
#if WINDOWS
        var purrExe = FindPurrExecutable();
        if (purrExe is not null)
            return await RunProcessAsync(purrExe, $"remove {packageName}", null);

        // Fallback: manually delete the package directory
        var packageDir = Path.Combine(PackagesDir, packageName);
        if (!Directory.Exists(packageDir))
            return (false, $"Package directory not found: {packageDir}");

        try
        {
            Directory.Delete(packageDir, recursive: true);
            return (true, $"Removed {packageName} from {PackagesDir}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to remove: {ex.Message}");
        }
#else
        return (false, "Uninstall is not supported on this platform.");
#endif
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<(bool Success, string Output)> RunProcessAsync(
        string executable,
        string arguments,
        IProgress<string>? progress)
    {
        var output = new System.Text.StringBuilder();

        var psi = new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            output.AppendLine(e.Data);
            progress?.Report(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            output.AppendLine(e.Data);
            progress?.Report(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return (process.ExitCode == 0, output.ToString());
    }

    private static string? FindPurrExecutable()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
#if WINDOWS
            foreach (var name in new[] { "purr.exe", "purr.cmd", "purr", "fur.exe", "fur.cmd", "fur" })
#else
            foreach (var name in new[] { "purr", "fur" })
#endif
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full)) return full;
            }
        }
        return null;
    }
}
