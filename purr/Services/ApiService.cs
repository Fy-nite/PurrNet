using System.Text.Json;
using Fur.Models;
using Fur.Utils;

namespace Fur.Services;

public class ApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string[] _baseUrls;
    // Changed to HTTP for local development - update this when deploying
    public const string BaseUrl = "http://purr.finite.ovh"; 

    public ApiService(string[] baseUrls)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _baseUrls = baseUrls;
    }

    // For backward compatibility
    public ApiService() : this(new[] { BaseUrl }) {}

    public async Task<FurConfig?> GetPackageInfoAsync(string packageName, string? version = null)
    {
        foreach (var baseUrl in _baseUrls)
        {
            var url = version != null
                ? $"{baseUrl}/api/v1/packages/{packageName}/{version}"
                : $"{baseUrl}/api/v1/packages/{packageName}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<FurConfig>(json);
                }
                else
                {
                    await HandleErrorResponse(response);
                }
            }
            catch (HttpRequestException ex)
            {
                ConsoleHelper.WriteError($"Network error: {ex.Message}");
                ConsoleHelper.WriteInfo($"Make sure the FUR API server at {baseUrl} is running");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Error fetching package info from {baseUrl}: {ex.Message}");
            }
        }
        return null;
    }

    public async Task<PackageListResponse?> GetPackagesAsync(string? sort = null, int page = 1, int pageSize = 50)
    {
        foreach (var baseUrl in _baseUrls)
        {
            var url = $"{baseUrl}/api/v1/packages?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(sort))
            {
                url += $"&sort={sort}";
            }

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<PackageListResponse>(json);
                }
                else
                {
                    await HandleErrorResponse(response);
                }
            }
            catch (HttpRequestException ex)
            {
                ConsoleHelper.WriteError($"Network error: {ex.Message}");
                ConsoleHelper.WriteInfo($"Make sure the FUR API server at {baseUrl} is running");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Error fetching packages from {baseUrl}: {ex.Message}");
            }
        }
        return null;
    }

    public async Task<PackageListResponse?> SearchPackagesAsync(string query, bool includeDetails = true)
    {
        foreach (var baseUrl in _baseUrls)
        {
            var url = $"{baseUrl}/api/v1/packages?search={Uri.EscapeDataString(query)}&details={includeDetails.ToString().ToLower()}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<PackageListResponse>(json);
                }
                else
                {
                    await HandleErrorResponse(response);
                }
            }
            catch (HttpRequestException ex)
            {
                ConsoleHelper.WriteError($"Network error: {ex.Message}");
                ConsoleHelper.WriteInfo($"Make sure the FUR API server at {baseUrl} is running");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Error searching packages from {baseUrl}: {ex.Message}");
            }
        }
        return null;
    }

    public async Task<RepositoryStatistics?> GetStatisticsAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            var url = $"{baseUrl}/api/v1/packages/statistics";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<RepositoryStatistics>(json);
                }
                else
                {
                    await HandleErrorResponse(response);
                }
            }
            catch (HttpRequestException ex)
            {
                ConsoleHelper.WriteError($"Network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Error fetching statistics from {baseUrl}: {ex.Message}");
            }
        }
        return null;
    }

    public async Task<string[]?> GetCategoriesAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            var url = $"{baseUrl}/api/v1/packages/categories";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<string[]>(json);
                }
                else
                {
                    await HandleErrorResponse(response);
                }
            }
            catch (HttpRequestException ex)
            {
                ConsoleHelper.WriteError($"Network error: {ex.Message}");
                ConsoleHelper.WriteInfo($"Make sure the FUR API server at {baseUrl} is running");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Error fetching categories from {baseUrl}: {ex.Message}");
            }
        }
        return null;
    }

    public async Task<PackageSearchResult[]?> GetPackagesByCategoryAsync(string category, bool includeDetails = true)
    {
        foreach (var baseUrl in _baseUrls)
        {
            var url = $"{baseUrl}/api/v1/packages/categories/{Uri.EscapeDataString(category)}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    // API returns an array of package-like objects; map to PackageSearchResult
                    return JsonSerializer.Deserialize<PackageSearchResult[]>(json);
                }
                else
                {
                    await HandleErrorResponse(response);
                }
            }
            catch (HttpRequestException ex)
            {
                ConsoleHelper.WriteError($"Network error: {ex.Message}");
                ConsoleHelper.WriteInfo($"Make sure the FUR API server at {baseUrl} is running");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Error fetching packages by category from {baseUrl}: {ex.Message}");
            }
        }
        return null;
    }

    public async Task TrackDownloadAsync(string packageName)
    {
        foreach (var baseUrl in _baseUrls)
        {
            var url = $"{baseUrl}/api/v1/packages/{packageName}/download";
            try
            {
                var response = await _httpClient.PostAsync(url, null);
                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Warning: Could not track download for {packageName} at {baseUrl}");
                }
            }
            catch
            {
                // Silently ignore download tracking errors
            }
        }
    }

    public async Task<HealthStatus?> CheckHealthAsync()
    {
        foreach (var baseUrl in _baseUrls)
        {
            var url = $"{baseUrl}/api/v1/health";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<HealthStatus>(json);
                }
            }
            catch
            {
                // Silently ignore health check errors
            }
        }
        return null;
    }

    private async Task HandleErrorResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        
        switch (response.StatusCode)
        {
            case System.Net.HttpStatusCode.NotFound:
                ConsoleHelper.WriteError("Package not found");
                break;
            case System.Net.HttpStatusCode.BadRequest:
                ConsoleHelper.WriteError($"Bad request: {content}");
                break;
            case System.Net.HttpStatusCode.Unauthorized:
                ConsoleHelper.WriteWarning("Unauthorized. This operation may require authentication");
                break;
            case System.Net.HttpStatusCode.TooManyRequests:
                ConsoleHelper.WriteWarning("Rate limited. Please try again later");
                break;
            default:
                ConsoleHelper.WriteError($"API returned {response.StatusCode}: {response.ReasonPhrase}");
                if (!string.IsNullOrEmpty(content))
                {
                    ConsoleHelper.WriteDim(content);
                    Console.WriteLine();
                }
                break;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
