using PurrLauncher.ViewModels;

namespace PurrLauncher.Pages;

public partial class InstalledPage : ContentPage
{
    private readonly InstalledViewModel _viewModel;

    public InstalledPage(InstalledViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadInstalled();
    }
}
