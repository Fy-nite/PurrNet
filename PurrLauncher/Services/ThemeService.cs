using PurrLauncher.Models;
using PurrLauncher.Resources.Themes;
using AppTheme = PurrLauncher.Models.PurrTheme;

namespace PurrLauncher.Services;

/// <summary>
/// Manages the active colour theme.  The chosen theme is persisted via
/// <see cref="Preferences"/> so it survives app restarts.
///
/// How it works
/// ─────────────
/// <c>App.xaml</c> loads <c>Colors.xaml</c> (neutral greys + white/black) and
/// <c>Styles.xaml</c> (control templates).  The brand colours (<c>Primary</c>,
/// <c>Purrple</c>, <c>Magenta</c>, …) are injected by a theme-specific
/// <see cref="ResourceDictionary"/> that is added to
/// <c>Application.Current.Resources.MergedDictionaries</c> at startup.
///
/// Because later entries in <c>MergedDictionaries</c> shadow earlier ones,
/// swapping the theme dict at runtime then re-creating the shell causes every
/// newly instantiated control to resolve the new colours via its style.
/// </summary>
public sealed class ThemeService
{
    private const string PreferenceKey = "purr_theme";

    // Kept so we can remove it before adding the replacement.
    private ResourceDictionary? _activeThemeDict;

    // Stored so ApplyTheme can rebuild the shell from DI.
    private IServiceProvider? _services;

    // ── Public API ────────────────────────────────────────────────────────

    public PurrTheme CurrentTheme { get; private set; } = PurrTheme.Purrple;

    /// <summary>
    /// Call once from <c>App.xaml.cs</c> AFTER <c>InitializeComponent()</c>
    /// to inject the saved (or default) colours before the shell is built.
    /// </summary>
    public void InitializeTheme(Application app, IServiceProvider services)
    {
        _services = services;
        var saved  = Preferences.Get(PreferenceKey, PurrTheme.Purrple.ToString());
        CurrentTheme = Enum.TryParse<PurrTheme>(saved, out var t) ? t : PurrTheme.Purrple;

        var dict = BuildDictionary(CurrentTheme);
        _activeThemeDict = dict;
        app.Resources.MergedDictionaries.Add(dict);
    }

    /// <summary>
    /// Switches to <paramref name="theme"/>, persists the preference and
    /// rebuilds the application shell so all controls re-resolve colours.
    /// </summary>
    public void ApplyTheme(PurrTheme theme)
    {
        if (Application.Current is null) return;

        CurrentTheme = theme;
        Preferences.Set(PreferenceKey, theme.ToString());

        // Swap the theme dictionary.
        var resources = Application.Current.Resources;
        if (_activeThemeDict is not null)
            resources.MergedDictionaries.Remove(_activeThemeDict);

        _activeThemeDict = BuildDictionary(theme);
        resources.MergedDictionaries.Add(_activeThemeDict);

        // Recreate the shell so every page is instantiated fresh with the new colours.
        if (_services is not null)
            Application.Current.MainPage = _services.GetRequiredService<AppShell>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static ResourceDictionary BuildDictionary(PurrTheme theme) =>
        theme switch
        {
            PurrTheme.Ocean    => new OceanTheme(),
            PurrTheme.Forest   => new ForestTheme(),
            PurrTheme.Crimson  => new CrimsonTheme(),
            PurrTheme.Midnight => new MidnightTheme(),
            _                  => new PurrpleTheme(),
        };
}
