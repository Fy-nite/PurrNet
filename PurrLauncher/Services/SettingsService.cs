using System.Collections.ObjectModel;
using PurrLauncher.Models;

namespace PurrLauncher.Services;

/// <summary>
/// Single source of truth for all user-configurable preferences.
/// Values are persisted via <see cref="Preferences"/> (key-value store backed
/// by the platform's own settings storage) so they survive app restarts.
/// </summary>
public sealed class SettingsService
{
    // ── Preference keys ───────────────────────────────────────────────────
    private const string KeyRepos          = "settings_repos";
    private const string KeyActiveRepo     = "settings_active_repo";
    private const string KeyPageSize       = "settings_page_size";
    private const string KeyTimeoutSeconds = "settings_timeout";
    private const string KeyInstallDir     = "settings_install_dir";
    private const string KeyCliPath        = "settings_cli_path";
    private const string KeyAutoRefresh    = "settings_auto_refresh";
    private const string KeyShowPrereleases= "settings_prereleases";
    private const string KeyCacheMinutes   = "settings_cache_minutes";

    // ── Defaults ─────────────────────────────────────────────────────────
    public const  string DefaultRepoName   = "PurrNet (official)";
    public const  string DefaultRepoUrl    = "https://purr.finite.ovh";
    public const  int    DefaultPageSize   = 50;
    public const  int    DefaultTimeout    = 30;
    public const  int    DefaultCache      = 5;

    // ── In-memory repo list (kept in sync with Preferences) ──────────────
    public ObservableCollection<RepoEntry> Repositories { get; } = new();

    // ── Index of the currently-active repo in Repositories ───────────────
    private int _activeRepoIndex;
    public int ActiveRepoIndex
    {
        get => _activeRepoIndex;
        set
        {
            _activeRepoIndex = Math.Clamp(value, 0, Math.Max(0, Repositories.Count - 1));
            Preferences.Set(KeyActiveRepo, _activeRepoIndex);
        }
    }

    public RepoEntry ActiveRepo =>
        Repositories.Count > 0
            ? Repositories[Math.Clamp(_activeRepoIndex, 0, Repositories.Count - 1)]
            : new RepoEntry(DefaultRepoName, DefaultRepoUrl);

    // ── Network ──────────────────────────────────────────────────────────
    public int RequestTimeoutSeconds
    {
        get => Preferences.Get(KeyTimeoutSeconds, DefaultTimeout);
        set => Preferences.Set(KeyTimeoutSeconds, Math.Clamp(value, 5, 120));
    }

    public int PackagePageSize
    {
        get => Preferences.Get(KeyPageSize, DefaultPageSize);
        set => Preferences.Set(KeyPageSize, Math.Clamp(value, 10, 500));
    }

    public int CacheMinutes
    {
        get => Preferences.Get(KeyCacheMinutes, DefaultCache);
        set => Preferences.Set(KeyCacheMinutes, Math.Clamp(value, 0, 60));
    }

    // ── Behaviour ────────────────────────────────────────────────────────
    /// <summary>Automatically load packages when Browse tab is opened.</summary>
    public bool AutoRefreshOnOpen
    {
        get => Preferences.Get(KeyAutoRefresh, true);
        set => Preferences.Set(KeyAutoRefresh, value);
    }

    /// <summary>Include pre-release/beta packages in browse results.</summary>
    public bool ShowPrereleases
    {
        get => Preferences.Get(KeyShowPrereleases, false);
        set => Preferences.Set(KeyShowPrereleases, value);
    }

    // ── Install ──────────────────────────────────────────────────────────
    /// <summary>
    /// Custom install directory.  Empty string → use the default
    /// (<c>~/.purr/packages</c>).
    /// </summary>
    public string CustomInstallDir
    {
        get => Preferences.Get(KeyInstallDir, string.Empty);
        set => Preferences.Set(KeyInstallDir, value.Trim());
    }

    /// <summary>
    /// Absolute path to the <c>purr</c> CLI executable.  Empty → auto-detect
    /// from PATH.
    /// </summary>
    public string CustomCliPath
    {
        get => Preferences.Get(KeyCliPath, string.Empty);
        set => Preferences.Set(KeyCliPath, value.Trim());
    }

    // ── Constructor ──────────────────────────────────────────────────────
    public SettingsService()
    {
        LoadRepos();
        _activeRepoIndex = Preferences.Get(KeyActiveRepo, 0);
        _activeRepoIndex = Math.Clamp(_activeRepoIndex, 0, Math.Max(0, Repositories.Count - 1));
    }

    // ── Repo management ──────────────────────────────────────────────────

    public void AddRepo(RepoEntry entry)
    {
        Repositories.Add(entry);
        SaveRepos();
    }

    public void RemoveRepo(RepoEntry entry)
    {
        var idx = Repositories.IndexOf(entry);
        if (idx < 0) return;

        Repositories.Remove(entry);

        // Keep active index in bounds.
        if (_activeRepoIndex >= Repositories.Count)
            ActiveRepoIndex = Math.Max(0, Repositories.Count - 1);

        SaveRepos();
    }

    public void MoveActiveRepo(RepoEntry entry)
    {
        var idx = Repositories.IndexOf(entry);
        if (idx >= 0) ActiveRepoIndex = idx;
    }

    public void ResetToDefaults()
    {
        Preferences.Remove(KeyTimeoutSeconds);
        Preferences.Remove(KeyPageSize);
        Preferences.Remove(KeyCacheMinutes);
        Preferences.Remove(KeyAutoRefresh);
        Preferences.Remove(KeyShowPrereleases);
        Preferences.Remove(KeyInstallDir);
        Preferences.Remove(KeyCliPath);

        Repositories.Clear();
        Repositories.Add(new RepoEntry(DefaultRepoName, DefaultRepoUrl));
        ActiveRepoIndex = 0;
        SaveRepos();
    }

    // ── Persistence helpers ───────────────────────────────────────────────

    /// <summary>
    /// Repos are stored as a newline-separated list of <c>Name|Url</c> tokens.
    /// </summary>
    private void SaveRepos()
    {
        var encoded = string.Join("\n", Repositories.Select(r => r.ToString()));
        Preferences.Set(KeyRepos, encoded);
    }

    private void LoadRepos()
    {
        Repositories.Clear();

        var raw = Preferences.Get(KeyRepos, string.Empty);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            foreach (var line in raw.Split('\n'))
            {
                var entry = RepoEntry.TryParse(line);
                if (entry is not null)
                    Repositories.Add(entry);
            }
        }

        // Always ensure the default repo is present.
        if (Repositories.Count == 0)
            Repositories.Add(new RepoEntry(DefaultRepoName, DefaultRepoUrl));
    }
}
