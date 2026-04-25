using PurrLauncher.Models;
using PurrLauncher.Services;
using PurrLauncher.ViewModels;

namespace PurrLauncher.Pages;

public partial class BrowsePage : ContentPage
{
    private readonly BrowseViewModel _viewModel;
    private readonly SettingsService _settings;

    public BrowsePage(BrowseViewModel viewModel, SettingsService settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings  = settings;
        BindingContext = viewModel;

        // Navigate to the detail page when a package is selected
        _viewModel.PackageSelected += async package =>
        {
            await Shell.Current.GoToAsync(
                $"{nameof(PackageDetailPage)}?name={Uri.EscapeDataString(package.Name)}");
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Honour the AutoRefreshOnOpen setting — only auto-load when enabled
        // and the list is currently empty.
        if (_viewModel.Packages.Count == 0 && _settings.AutoRefreshOnOpen)
            _viewModel.LoadPackagesCommand.Execute(null);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PackageInfo package)
        {
            PackagesList.SelectedItem = null;   // reset highlight
            _viewModel.SelectPackage(package);
        }
    }
}
