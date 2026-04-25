using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using PurrLauncher.Models;
using PurrLauncher.Services;
using AppTheme = PurrLauncher.Models.PurrTheme;

namespace PurrLauncher.ViewModels;

/// <summary>Represents a single theme choice on the Settings page.</summary>
public sealed class ThemeOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public PurrTheme Theme        { get; init; }
    public string   Name         { get; init; } = string.Empty;
    public string   Emoji        { get; init; } = string.Empty;
    /// <summary>Accent colour shown in the swatch (light-mode shade).</summary>
    public Color    AccentLight  { get; init; }
    /// <summary>Accent colour shown in the swatch (dark-mode shade).</summary>
    public Color    AccentDark   { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class SettingsViewModel : BaseViewModel
{
    private readonly ThemeService    _themeService;
    private readonly SettingsService _settings;

    private ThemeOption? _selectedTheme;

    // ── New-repo entry fields ─────────────────────────────────────────────
    private string _newRepoName = string.Empty;
    private string _newRepoUrl  = string.Empty;

    // ── Editable copies of settings (applied on Save) ─────────────────────
    private int    _pageSize;
    private int    _timeoutSeconds;
    private int    _cacheMinutes;
    private bool   _autoRefresh;
    private bool   _showPrereleases;
    private string _customInstallDir = string.Empty;
    private string _customCliPath    = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────
    public SettingsViewModel(ThemeService themeService, SettingsService settings)
    {
        _themeService = themeService;
        _settings     = settings;
        Title = "Settings";

        // ── Theme options ──────────────────────────────────────────────
        Themes = new ObservableCollection<ThemeOption>
        {
            new() { Theme = PurrTheme.Purrple,  Name = "Purrple",  Emoji = "🟣",
                    AccentLight = Color.FromArgb("#7C3AED"), AccentDark = Color.FromArgb("#a78bfa") },
            new() { Theme = PurrTheme.Ocean,    Name = "Ocean",    Emoji = "🔵",
                    AccentLight = Color.FromArgb("#0284C7"), AccentDark = Color.FromArgb("#38BDF8") },
            new() { Theme = PurrTheme.Forest,   Name = "Forest",   Emoji = "🟢",
                    AccentLight = Color.FromArgb("#16A34A"), AccentDark = Color.FromArgb("#4ADE80") },
            new() { Theme = PurrTheme.Crimson,  Name = "Crimson",  Emoji = "🔴",
                    AccentLight = Color.FromArgb("#DC2626"), AccentDark = Color.FromArgb("#F87171") },
            new() { Theme = PurrTheme.Midnight, Name = "Midnight", Emoji = "⚫",
                    AccentLight = Color.FromArgb("#475569"), AccentDark = Color.FromArgb("#94A3B8") },
        };

        _selectedTheme = Themes.FirstOrDefault(t => t.Theme == _themeService.CurrentTheme)
                         ?? Themes[0];
        _selectedTheme.IsSelected = true;

        // ── Load settings into editable fields ────────────────────────
        _pageSize        = _settings.PackagePageSize;
        _timeoutSeconds  = _settings.RequestTimeoutSeconds;
        _cacheMinutes    = _settings.CacheMinutes;
        _autoRefresh     = _settings.AutoRefreshOnOpen;
        _showPrereleases = _settings.ShowPrereleases;
        _customInstallDir = _settings.CustomInstallDir;
        _customCliPath   = _settings.CustomCliPath;

        // ── Commands ──────────────────────────────────────────────────
        SelectThemeCommand    = new Command<ThemeOption>(OnSelectTheme);
        AddRepoCommand        = new Command(OnAddRepo, CanAddRepo);
        RemoveRepoCommand     = new Command<RepoEntry>(OnRemoveRepo);
        SetActiveRepoCommand  = new Command<RepoEntry>(OnSetActiveRepo);
        SaveNetworkCommand    = new Command(OnSaveNetwork);
        SaveBehaviourCommand  = new Command(OnSaveBehaviour);
        SaveInstallCommand    = new Command(OnSaveInstall);
        ResetDefaultsCommand  = new Command(OnResetDefaults);
    }

    // ── Theme ─────────────────────────────────────────────────────────────
    public ObservableCollection<ThemeOption> Themes { get; }

    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    public ICommand SelectThemeCommand { get; }

    // ── Repositories ──────────────────────────────────────────────────────
    /// <summary>Live list exposed to the UI (same object owned by SettingsService).</summary>
    public ObservableCollection<RepoEntry> Repositories => _settings.Repositories;

    public int ActiveRepoIndex
    {
        get => _settings.ActiveRepoIndex;
        set
        {
            _settings.ActiveRepoIndex = value;
            OnPropertyChanged();
        }
    }

    public string NewRepoName
    {
        get => _newRepoName;
        set
        {
            SetProperty(ref _newRepoName, value);
            ((Command)AddRepoCommand).ChangeCanExecute();
        }
    }

    public string NewRepoUrl
    {
        get => _newRepoUrl;
        set
        {
            SetProperty(ref _newRepoUrl, value);
            ((Command)AddRepoCommand).ChangeCanExecute();
        }
    }

    public ICommand AddRepoCommand       { get; }
    public ICommand RemoveRepoCommand    { get; }
    public ICommand SetActiveRepoCommand { get; }

    // ── Network settings ──────────────────────────────────────────────────
    public int PageSize
    {
        get => _pageSize;
        set => SetProperty(ref _pageSize, value);
    }

    public int TimeoutSeconds
    {
        get => _timeoutSeconds;
        set => SetProperty(ref _timeoutSeconds, value);
    }

    public int CacheMinutes
    {
        get => _cacheMinutes;
        set => SetProperty(ref _cacheMinutes, value);
    }

    public ICommand SaveNetworkCommand { get; }

    // ── Behaviour settings ────────────────────────────────────────────────
    public bool AutoRefresh
    {
        get => _autoRefresh;
        set => SetProperty(ref _autoRefresh, value);
    }

    public bool ShowPrereleases
    {
        get => _showPrereleases;
        set => SetProperty(ref _showPrereleases, value);
    }

    public ICommand SaveBehaviourCommand { get; }

    // ── Install settings ──────────────────────────────────────────────────
    public string CustomInstallDir
    {
        get => _customInstallDir;
        set => SetProperty(ref _customInstallDir, value);
    }

    public string CustomCliPath
    {
        get => _customCliPath;
        set => SetProperty(ref _customCliPath, value);
    }

    public ICommand SaveInstallCommand  { get; }
    public ICommand ResetDefaultsCommand { get; }

    // ── Command implementations ───────────────────────────────────────────

    private void OnSelectTheme(ThemeOption option)
    {
        if (option is null || option.Theme == _themeService.CurrentTheme) return;

        if (_selectedTheme is not null)
            _selectedTheme.IsSelected = false;

        option.IsSelected = true;
        SelectedTheme = option;
        _themeService.ApplyTheme(option.Theme);
    }

    private bool CanAddRepo() =>
        !string.IsNullOrWhiteSpace(_newRepoName) &&
        !string.IsNullOrWhiteSpace(_newRepoUrl)  &&
        Uri.IsWellFormedUriString(_newRepoUrl.Trim(), UriKind.Absolute);

    private void OnAddRepo()
    {
        _settings.AddRepo(new RepoEntry(_newRepoName.Trim(), _newRepoUrl.Trim()));
        NewRepoName = string.Empty;
        NewRepoUrl  = string.Empty;
    }

    private void OnRemoveRepo(RepoEntry? entry)
    {
        if (entry is null) return;
        if (Repositories.Count <= 1)
        {
            Shell.Current.DisplayAlert("Cannot remove", "You must keep at least one repository.", "OK");
            return;
        }
        _settings.RemoveRepo(entry);
        OnPropertyChanged(nameof(ActiveRepoIndex));
    }

    private void OnSetActiveRepo(RepoEntry? entry)
    {
        if (entry is null) return;
        _settings.MoveActiveRepo(entry);
        OnPropertyChanged(nameof(ActiveRepoIndex));
    }

    private void OnSaveNetwork()
    {
        _settings.PackagePageSize       = _pageSize;
        _settings.RequestTimeoutSeconds = _timeoutSeconds;
        _settings.CacheMinutes          = _cacheMinutes;
        Shell.Current.DisplayAlert("Saved", "Network settings saved.", "OK");
    }

    private void OnSaveBehaviour()
    {
        _settings.AutoRefreshOnOpen = _autoRefresh;
        _settings.ShowPrereleases   = _showPrereleases;
        Shell.Current.DisplayAlert("Saved", "Behaviour settings saved.", "OK");
    }

    private void OnSaveInstall()
    {
        _settings.CustomInstallDir = _customInstallDir;
        _settings.CustomCliPath    = _customCliPath;
        Shell.Current.DisplayAlert("Saved", "Install settings saved.", "OK");
    }

    private void OnResetDefaults()
    {
        _settings.ResetToDefaults();

        PageSize        = SettingsService.DefaultPageSize;
        TimeoutSeconds  = SettingsService.DefaultTimeout;
        CacheMinutes    = SettingsService.DefaultCache;
        AutoRefresh     = true;
        ShowPrereleases = false;
        CustomInstallDir = string.Empty;
        CustomCliPath    = string.Empty;

        OnPropertyChanged(nameof(ActiveRepoIndex));
        Shell.Current.DisplayAlert("Reset", "All settings restored to defaults.", "OK");
    }
}

