using System.Collections.ObjectModel;
using System.Windows.Input;
using PurrLauncher.Models;
using PurrLauncher.Services;

namespace PurrLauncher.ViewModels;

public class BrowseViewModel : BaseViewModel
{
    private readonly PurrApiService _apiService;

    // Shared set – also used by PackageDetailViewModel on Android.
    public static readonly HashSet<string> AndroidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "android", "mobile", "phone", "apk", "app"
    };

    private string _searchQuery = string.Empty;
    private bool _showingDesktop;
    private List<PackageInfo> _allPackages = new();

    public ObservableCollection<PackageInfo> Packages { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    /// <summary>True when the Desktop tab is active (Android only).</summary>
    public bool ShowingDesktop
    {
        get => _showingDesktop;
        set
        {
            SetProperty(ref _showingDesktop, value);
            OnPropertyChanged(nameof(MobileTabSelected));
            OnPropertyChanged(nameof(DesktopTabSelected));
            OnPropertyChanged(nameof(ActiveBannerText));
            ApplyFilter();
        }
    }

    public bool MobileTabSelected => !_showingDesktop;
    public bool DesktopTabSelected => _showingDesktop;

    /// <summary>True when running on Android – drives tab bar and banner visibility.</summary>
    public bool IsAndroidMode
    {
#if ANDROID
        get => true;
#else
        get => false;
#endif
    }

    public string ActiveBannerText =>
        _showingDesktop
            ? "🖥  Desktop apps – may not work on Android"
            : "📱  Showing Android & Mobile packages";

    public ICommand LoadPackagesCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ShowMobileTabCommand { get; }
    public ICommand ShowDesktopTabCommand { get; }

    // Raised by the page to navigate when an item is tapped
    public event Action<PackageInfo>? PackageSelected;

    public BrowseViewModel(PurrApiService apiService)
    {
        _apiService = apiService;

#if ANDROID
        Title = "Browse";
#else
        Title = "Browse Packages";
#endif

        LoadPackagesCommand = new Command(async () => await LoadPackagesAsync());
        SearchCommand = new Command(async () => await SearchAsync());
        ShowMobileTabCommand = new Command(() => ShowingDesktop = false);
        ShowDesktopTabCommand = new Command(() => ShowingDesktop = true);
    }

    public async Task LoadPackagesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            _allPackages.Clear();
            var result = await _apiService.GetPackagesAsync(pageSize: 100, details: true);
            _allPackages = TagPackages(result?.DetailedPackages ?? Array.Empty<PackageInfo>()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            await ShowError("Failed to load packages", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SearchAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await LoadPackagesAsync();
            return;
        }

        IsBusy = true;
        try
        {
            _allPackages.Clear();
            var result = await _apiService.SearchPackagesAsync(SearchQuery);
            _allPackages = TagPackages(result?.DetailedPackages ?? Array.Empty<PackageInfo>()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            await ShowError("Search failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Sets <see cref="PackageInfo.IsDesktopWarning"/> on each package and returns them.
    /// A package is flagged as desktop when it has NO mobile/android category.
    /// </summary>
    private static IEnumerable<PackageInfo> TagPackages(IEnumerable<PackageInfo> packages)
    {
        foreach (var pkg in packages)
        {
#if ANDROID
            pkg.IsDesktopWarning = pkg.Categories.Length > 0
                && !pkg.Categories.Any(c => AndroidCategories.Contains(c));
#else
            pkg.IsDesktopWarning = false;
#endif
            yield return pkg;
        }
    }

    /// <summary>Refills <see cref="Packages"/> from the cached list, respecting the active tab.</summary>
    private void ApplyFilter()
    {
        Packages.Clear();

#if ANDROID
        var filtered = _showingDesktop
            ? _allPackages.Where(p => p.IsDesktopWarning)
            : _allPackages.Where(p => !p.IsDesktopWarning);
        foreach (var pkg in filtered)
            Packages.Add(pkg);
#else
        foreach (var pkg in _allPackages)
            Packages.Add(pkg);
#endif
    }

    public void SelectPackage(PackageInfo? package)
    {
        if (package is not null)
            PackageSelected?.Invoke(package);
    }

    private static Task ShowError(string title, string message)
        => Shell.Current.DisplayAlert(title, message, "OK");
}
