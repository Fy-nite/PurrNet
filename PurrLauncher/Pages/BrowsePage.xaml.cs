using PurrLauncher.Models;
using PurrLauncher.ViewModels;

namespace PurrLauncher.Pages;

public partial class BrowsePage : ContentPage
{
    private readonly BrowseViewModel _viewModel;

    public BrowsePage(BrowseViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
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
        if (_viewModel.Packages.Count == 0)
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
