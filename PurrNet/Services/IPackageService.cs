using Purrnet.Models;

namespace Purrnet.Services
{
    public interface IPackageService
    {
        // Package CRUD operations
        Task<List<Package>> GetAllPackagesAsync();
        Task<Package?> GetPackageAsync(string packageName, string? version = null);
        Task<Package?> GetPackageByIdAsync(int id);
        Task<bool> SavePackageAsync(PurrConfig PurrConfig, string createdBy, int? ownerId = null);
        Task<bool> SavePackageAsync(PurrConfig PurrConfig, string createdBy); // Overload for backward compatibility
        Task<bool> UpdatePackageAsync(int id, PurrConfig PurrConfig, string? updatedBy = null);
        Task<bool> DeletePackageAsync(int id);
        Task<bool> TogglePackageStatusAsync(int id);

        // Search and filtering
        Task<SearchResult> SearchPackagesAsync(string? query = null, string? sort = null, int page = 1, int pageSize = 20);
        Task<PackageListResponse> GetPackageListAsync(string? sort = null, string? search = null, bool includeDetails = false);
        Task<List<Package>> GetPackagesByTagAsync(string tag);
        Task<List<Package>> GetPackagesByAuthorAsync(string author);
        Task<List<Package>> GetPackagesByCategoryAsync(string category);

        // Statistics and analytics
        Task<PackageStatistics> GetStatisticsAsync();
        Task<bool> IncrementDownloadCountAsync(int packageId);
        Task<bool> IncrementViewCountAsync(int packageId);
        Task<List<string>> GetPopularTagsAsync(int limit = 10);
        Task<List<string>> GetPopularCategoriesAsync(int limit = 10);
        Task<List<string>> GetPopularAuthorsAsync(int limit = 10);

        // Database management
        Task<bool> InitializeDatabaseAsync();
        Task<bool> ClearAllDataAsync();
        Task<bool> ImportPackagesFromJsonAsync(string jsonFilePath);
        Task<bool> ExportPackagesToJsonAsync(string jsonFilePath);
        Task<int> GetPackageCountAsync();
        Task<bool> MigrateCategoriesAsync();
    }
}
