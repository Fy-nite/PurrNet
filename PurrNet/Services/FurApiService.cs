using Purrnet.Models;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Purrnet.Services
{
    public class PurrApiService : IPurrApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PurrApiService> _logger;
        private readonly IMemoryCache _cache;
        private readonly ILocalPackageService _localPackageService;
        private readonly string _baseUrl;
        private readonly bool _useLocalStorageWhenOffline;
        private const string CACHE_KEY_PACKAGES = "cached_packages";
        private const string CACHE_KEY_PACKAGE_COUNT = "cached_package_count";
        private const string CACHE_KEY_PACKAGE_DETAILS = "cached_package_details";
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public bool IsApiAvailable { get; private set; } = true;
        public bool UseLocalStorage => !IsApiAvailable && _useLocalStorageWhenOffline;

        public PurrApiService(HttpClient httpClient, ILogger<PurrApiService> logger, IMemoryCache cache, 
            ILocalPackageService localPackageService, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            _localPackageService = localPackageService;
            _baseUrl = configuration.GetValue<string>("ApiSettings:BaseUrl") ?? "http://localhost:5001";
            _useLocalStorageWhenOffline = configuration.GetValue<bool>("ApiSettings:UseLocalStorageWhenOffline", true);
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<PackageListResponse?> GetPackagesAsync(string? sort = null, string? search = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (!string.IsNullOrEmpty(sort)) queryParams.Add($"sort={Uri.EscapeDataString(sort)}");
                if (!string.IsNullOrEmpty(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");
                
                var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                var response = await _httpClient.GetAsync($"/api/v1/packages{query}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<PackageListResponse>(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                }
                IsApiAvailable = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching packages from API");
                IsApiAvailable = false;
            }

            return _useLocalStorageWhenOffline ? await _localPackageService.GetPackageListAsync(sort, search, false) : GetCachedPackageResponse(sort, search);
        }

        public async Task<PurrConfig?> GetPackageAsync(string packageName, string? version = null)
        {
            try
            {
                var url = version != null ? $"/api/v1/packages/{Uri.EscapeDataString(packageName)}/{Uri.EscapeDataString(version)}" : $"/api/v1/packages/{Uri.EscapeDataString(packageName)}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<PurrConfig>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching package {PackageName} from API", packageName);
            }
            return _useLocalStorageWhenOffline ? await _localPackageService.GetPackageAsync(packageName, version) : null;
        }

        public async Task<bool> UploadPackageAsync(PurrConfig PurrConfig)
        {
            try
            {
                var json = JsonSerializer.Serialize(PurrConfig, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                var response = await _httpClient.PostAsync("/api/v1/packages", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
                if (response.IsSuccessStatusCode) return true;
            }
            catch (Exception ex) { _logger.LogError(ex, "Error uploading package {PackageName} to API", PurrConfig.Name); }
            return _useLocalStorageWhenOffline && await _localPackageService.SavePackageAsync(PurrConfig);
        }

        public async Task<bool> IsApiHealthyAsync()
        {
            try { return (await _httpClient.GetAsync("/health")).IsSuccessStatusCode; }
            catch { return false; }
        }

        public async Task<List<Package>> GetPackageDetailsAsync(string? sort = null, string? search = null)
        {
            try
            {
                var query = $"?sort={Uri.EscapeDataString(sort ?? "")}&search={Uri.EscapeDataString(search ?? "")}&details=true";
                var response = await _httpClient.GetAsync($"/api/v1/packages{query}");
                if (response.IsSuccessStatusCode)
                {
                    var packageResponse = JsonSerializer.Deserialize<PackageListResponse>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                    return packageResponse?.PackageDetails?.Select(p => new Package {
                        Name = p.Name,
                        Version = p.Version,
                        Authors = JsonSerializer.Serialize(p.Authors),
                        SupportedPlatforms = JsonSerializer.Serialize(p.SupportedPlatforms),
                        Description = p.Description,
                        ReadmeUrl = p.ReadmeUrl,
                        License = p.License,
                        LicenseUrl = p.LicenseUrl,
                        Keywords = JsonSerializer.Serialize(p.Keywords),
                        Dependencies = JsonSerializer.Serialize(p.Dependencies),
                        LastUpdated = DateTime.UtcNow.ToString("O")
                    }).ToList() ?? new List<Package>();
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "API Error"); }
            return new List<Package>();
        }

        public void ClearCache() { }
        private PackageListResponse? GetCachedPackageResponse(string? sort, string? search) => null;
    }
}
