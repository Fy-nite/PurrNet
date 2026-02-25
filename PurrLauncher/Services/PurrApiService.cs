using System.Text.Json;
using PurrLauncher.Models;

namespace PurrLauncher.Services;

public class PurrApiService
{
    private readonly HttpClient _httpClient;
    public const string BaseUrl = "https://purr.finite.ovh";

    public PurrApiService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>Lists packages, with optional search query and pagination.</summary>
    public async Task<PackageListResult?> GetPackagesAsync(
        int page = 1,
        int pageSize = 50,
        string? search = null,
        bool details = true)
    {
        var url = $"{BaseUrl}/api/v1/packages?page={page}&pageSize={pageSize}&details={details.ToString().ToLower()}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"&search={Uri.EscapeDataString(search)}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PackageListResult>(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PurrApiService] GetPackages error: {ex.Message}");
        }
        return null;
    }

    /// <summary>Fetches full metadata for a single package.</summary>
    public async Task<PackageInfo?> GetPackageInfoAsync(string name, string? version = null)
    {
        var url = version != null
            ? $"{BaseUrl}/api/v1/packages/{Uri.EscapeDataString(name)}/{Uri.EscapeDataString(version)}"
            : $"{BaseUrl}/api/v1/packages/{Uri.EscapeDataString(name)}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PackageInfo>(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PurrApiService] GetPackageInfo error: {ex.Message}");
        }
        return null;
    }

    /// <summary>Searches packages by query string.</summary>
    public async Task<PackageListResult?> SearchPackagesAsync(string query)
        => await GetPackagesAsync(search: query, pageSize: 100, details: true);
}
