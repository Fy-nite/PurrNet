using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using PurrLauncher.Models;
using PurrLauncher.Services;

namespace PurrLauncher.ViewModels;

public class PackageDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly PurrApiService _apiService;
    private readonly PackageInstallService _installService;

    private string _packageName = string.Empty;
    private PackageInfo? _package;
    private bool _isInstalled;
    private bool _isInstalling;
    private string _statusMessage = string.Empty;
    private string _outputLog = string.Empty;

    public PackageInfo? Package
    {
        get => _package;
        set
        {
            SetProperty(ref _package, value);
            OnPropertyChanged(nameof(HasPackage));
            OnPropertyChanged(nameof(InstallButtonText));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(IsDesktopPackage));
            OnPropertyChanged(nameof(ShowDesktopWarning));
        }
    }

    public bool HasPackage => _package is not null;

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            SetProperty(ref _isInstalled, value);
            OnPropertyChanged(nameof(InstallButtonText));
            OnPropertyChanged(nameof(ShowUninstallButton));
        }
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        set
        {
            SetProperty(ref _isInstalling, value);
            OnPropertyChanged(nameof(CanInteract));
        }
    }

    public bool CanInteract => !IsInstalling && !IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            SetProperty(ref _statusMessage, value);
            OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);

    public string OutputLog
    {
        get => _outputLog;
        set => SetProperty(ref _outputLog, value);
    }

    // Windows: "Install" / "Installed ✓"  |  Android: "Get Package →"
    public string InstallButtonText
    {
#if ANDROID
        get => "Get Package →";
#else
        get => IsInstalled ? "Installed ✓" : "⬇  Install";
#endif
    }

    // On Windows, show a separate "Uninstall" button only when the package is installed
    public bool ShowUninstallButton
    {
#if WINDOWS
        get => IsInstalled;
#else
        get => false;
#endif
    }

    // The primary action button label (used when we collapse install+get into one button)
    public string ActionButtonText => InstallButtonText;

    /// <summary>True on Android when this package has no mobile/android categories.</summary>
    public bool IsDesktopPackage
    {
#if ANDROID
        get => _package is not null
            && _package.Categories.Length > 0
            && !_package.Categories.Any(c => BrowseViewModel.AndroidCategories.Contains(c));
#else
        get => false;
#endif
    }

    /// <summary>Drives the warning banner on the detail page (Android only).</summary>
    public bool ShowDesktopWarning => IsDesktopPackage;

    public ICommand InstallOrGetCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand OpenHomepageCommand { get; }
    public ICommand OpenReleasesCommand { get; }

    public PackageDetailViewModel(PurrApiService apiService, PackageInstallService installService)
    {
        _apiService = apiService;
        _installService = installService;

        InstallOrGetCommand = new Command(async () => await InstallOrGetAsync());
        UninstallCommand = new Command(async () => await UninstallAsync());
        OpenHomepageCommand = new Command(async () => await OpenUrlAsync(_package?.Homepage));
        OpenReleasesCommand = new Command(async () => await OpenUrlAsync(_package?.ReleasesUrl));
    }

    // ─── IQueryAttributable ───────────────────────────────────────────────────

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("name", out var raw))
        {
            _packageName = Uri.UnescapeDataString(raw?.ToString() ?? string.Empty);
            _ = LoadAsync();
        }
    }

    // ─── Load ─────────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_packageName)) return;
        IsBusy = true;
        StatusMessage = string.Empty;
        OutputLog = string.Empty;
        try
        {
            Package = await _apiService.GetPackageInfoAsync(_packageName);
            if (Package is not null)
            {
                Title = Package.Name;
                IsInstalled = _installService.IsPackageInstalled(Package.Name);
            }
            else
            {
                StatusMessage = $"Package '{_packageName}' not found.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanInteract));
        }
    }

    // ─── Install / Get ────────────────────────────────────────────────────────

    private async Task InstallOrGetAsync()
    {
        if (Package is null || IsInstalling) return;

#if WINDOWS
        if (IsInstalled)
        {
            // Already installed – nothing to do; Uninstall has its own button
            StatusMessage = $"{Package.Name} is already installed.";
            return;
        }
        await DoInstallAsync();
#else
        // Android: if this is a desktop package, warn the user first
        if (IsDesktopPackage)
        {
            var confirmed = await Shell.Current.DisplayAlert(
                "⚠️ Desktop App",
                $"{Package.Name} is a desktop application and may not work on Android. Download anyway?",
                "Download anyway", "Cancel");
            if (!confirmed)
            {
                StatusMessage = "Download cancelled.";
                return;
            }
        }

        StatusMessage = "Opening releases page…";
        var (ok, msg) = await _installService.InstallPackageAsync(
            Package.Name,
            releasesUrl: Package.ReleasesUrl);
        StatusMessage = ok ? "✅ Opened in browser." : $"❌ {msg}";
#endif
    }

    private async Task DoInstallAsync()
    {
        if (Package is null) return;
        IsInstalling = true;
        StatusMessage = $"Installing {Package.Name}…";
        OutputLog = string.Empty;

        var lines = new System.Text.StringBuilder();
        var progress = new Progress<string>(line =>
        {
            lines.AppendLine(line);
            OutputLog = lines.ToString();
            StatusMessage = line;
        });

        try
        {
            var (success, output) = await _installService.InstallPackageAsync(
                Package.Name, releasesUrl: null, progress: progress);

            if (success)
            {
                IsInstalled = true;
                StatusMessage = $"✅ {Package.Name} installed successfully!";
            }
            else
            {
                StatusMessage = $"❌ Installation failed.";
                OutputLog = output;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
            OnPropertyChanged(nameof(CanInteract));
        }
    }

    // ─── Uninstall ────────────────────────────────────────────────────────────

    private async Task UninstallAsync()
    {
        if (Package is null || IsInstalling) return;

        var confirmed = await Shell.Current.DisplayAlert(
            "Uninstall",
            $"Remove '{Package.Name}' from your system?",
            "Yes, remove", "Cancel");
        if (!confirmed) return;

        IsInstalling = true;
        StatusMessage = $"Removing {Package.Name}…";
        try
        {
            var (success, output) = await _installService.UninstallPackageAsync(Package.Name);
            StatusMessage = success
                ? $"✅ {Package.Name} removed."
                : $"❌ {output}";
            if (success)
                IsInstalled = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
            OnPropertyChanged(nameof(CanInteract));
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task OpenUrlAsync(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try { await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred); }
        catch { /* ignore */ }
    }
}
