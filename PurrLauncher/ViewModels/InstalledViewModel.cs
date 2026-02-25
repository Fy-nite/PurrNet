using System.Collections.ObjectModel;
using System.Windows.Input;
using PurrLauncher.Models;
using PurrLauncher.Services;

namespace PurrLauncher.ViewModels;

public class InstalledViewModel : BaseViewModel
{
    private readonly PackageInstallService _installService;

    public ObservableCollection<InstalledPackage> InstalledPackages { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public InstalledViewModel(PackageInstallService installService)
    {
        _installService = installService;
        Title = "Installed Packages";

        RefreshCommand = new Command(LoadInstalled);
        UninstallCommand = new Command<InstalledPackage>(async p => await UninstallAsync(p));
        OpenFolderCommand = new Command<InstalledPackage>(OpenFolder);
    }

    public void LoadInstalled()
    {
        InstalledPackages.Clear();
        foreach (var pkg in _installService.GetInstalledPackages())
            InstalledPackages.Add(pkg);
    }

    private async Task UninstallAsync(InstalledPackage? package)
    {
        if (package is null) return;

        var confirmed = await Shell.Current.DisplayAlert(
            "Uninstall",
            $"Remove '{package.Name}'?",
            "Yes, remove", "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var (success, output) = await _installService.UninstallPackageAsync(package.Name);
            if (success)
            {
                InstalledPackages.Remove(package);
                await Shell.Current.DisplayAlert("Done", $"{package.Name} was removed.", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", output, "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void OpenFolder(InstalledPackage? package)
    {
        if (package is null || !Directory.Exists(package.InstallPath)) return;
#if WINDOWS
        System.Diagnostics.Process.Start("explorer.exe", package.InstallPath);
#endif
    }
}
