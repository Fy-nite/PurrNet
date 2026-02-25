using PurrLauncher.ViewModels;

namespace PurrLauncher.Pages;

[QueryProperty(nameof(PackageName), "name")]
public partial class PackageDetailPage : ContentPage
{
    private readonly PackageDetailViewModel _viewModel;

    // Shell passes the "name" query param via this property; the ViewModel
    // implements IQueryAttributable so it also receives it directly.
    public string? PackageName { get; set; }

    public PackageDetailPage(PackageDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }
}
