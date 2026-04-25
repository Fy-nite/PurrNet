using System.Text.Json;
using PurrLauncher.Models;

namespace PurrLauncher.Services;

public class PurrApiService
{
    private readonly SettingsService _settings;
    private HttpClient _httpClient;

    /// <summary>The fallback URL used when no repos are configured.</summary>
    public const string DefaultBaseUrl = "https://purr.finite.ovh";

    /// <summary>Current active base URL (from the active repo in settings).</summary>
    public string BaseUrl => _settings.ActiveRepo.Url.TrimEnd('/');

    public PurrApiService(SettingsService settings)
    {
        _settings = settings;
        _httpClient = BuildClient();
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Lists packages.  Tries the active repo first; if that fails, iterates
    /// all other configured repos as fallback.
    /// </summary>
    public async Task<PackageListResult?> GetPackagesAsync(
        int page = 1,
        int pageSize = -1,
        string? search = null,
        bool details = true)
    {
        // -1 means "use the value from settings"
        if (pageSize < 0) pageSize = _settings.PackagePageSize;

        // Refresh the client if timeout changed.
        _httpClient = BuildClient();

        // Try active repo, then fall through to others.
        var repos = GetRepoUrls();
        foreach (var baseUrl in repos)
        {
            var url = $"{baseUrl}/api/v1/packages?page={page}&pageSize={pageSize}&details={details.ToString().ToLower()}";
            if (!string.IsNullOrWhiteSpace(search))
                url += $"&search={Uri.EscapeDataString(search)}";

            var result = await TryGetJsonAsync<PackageListResult>(url);
            if (result is not null) return result;
        }
        return null;
    }

    /// <summary>Fetches full metadata for a single package from the active repo.</summary>
    public async Task<PackageInfo?> GetPackageInfoAsync(string name, string? version = null)
    {
        _httpClient = BuildClient();
        foreach (var baseUrl in GetRepoUrls())
        {
            var url = version != null
                ? $"{baseUrl}/api/v1/packages/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(version)}"
                : $"{baseUrl}/api/v1/packages/{Uri.EscapeDataString(name)}";

            var result = await TryGetJsonAsync<PackageInfo>(url);
            if (result is not null) return result;
        }
        return null;
    }

    /// <summary>Searches packages by query string.</summary>
    public async Task<PackageListResult?> SearchPackagesAsync(string query)
        => await GetPackagesAsync(search: query, pageSize: Math.Min(200, _settings.PackagePageSize * 2), details: true);

    // ── Helpers ───────────────────────────────────────────────────────────

    private HttpClient BuildClient() => new()
    {
        Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds)
    };

    /// <summary>
    /// Returns repo URLs with the active one first, then the rest in order.
    /// </summary>
    private IEnumerable<string> GetRepoUrls()
    {
        var repos = _settings.Repositories;
        var activeIdx = Math.Clamp(_settings.ActiveRepoIndex, 0, Math.Max(0, repos.Count - 1));

        // Active repo first.
        if (repos.Count > 0)
            yield return repos[activeIdx].Url.TrimEnd('/');

        // Remaining repos as fallback.
        for (var i = 0; i < repos.Count; i++)
        {
            if (i != activeIdx)
                yield return repos[i].Url.TrimEnd('/');
        }

        // Ultimate fallback to the hardcoded default.
        if (repos.Count == 0)
            yield return DefaultBaseUrl;
    }

    private async Task<T?> TryGetJsonAsync<T>(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PurrApiService] {url} → {ex.Message}");
        }
        return default;
    }
}
