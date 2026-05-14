using Microsoft.EntityFrameworkCore;
using Purrnet.Data;
using Purrnet.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Purrnet.Services
{
    public class PackageService : IPackageService
    {
        private readonly PurrNetDbContext _context;
        private readonly ILogger<PackageService> _logger;
        private readonly static string _sanitizeRegex = @"[^\x20-\x7e]+";

        public PackageService(PurrNetDbContext context, ILogger<PackageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Package>> GetAllPackagesAsync()
        {
            return await _context.Packages.OrderBy(p => p.Name).ToListAsync();
        }

        public async Task<Package?> GetPackageAsync(string packageName, string? version = null)
        {
            var query = _context.Packages.Where(p => p.Name == packageName && p.IsActive == 1);
            if (!string.IsNullOrEmpty(version)) query = query.Where(p => p.Version == version);
            return await query.FirstOrDefaultAsync();
        }

        public async Task<Package?> GetPackageByIdAsync(string id)
        {
            return await _context.Packages.FirstOrDefaultAsync(p => p.Id == int.Parse(id));
        }

        public async Task<bool> SavePackageAsync(PurrConfig purrConfig, string createdBy, string? ownerId = null)
        {
            var existing = await _context.Packages.FirstOrDefaultAsync(p => p.Name == purrConfig.Name);
            if (existing != null) return await UpdatePackageAsync(existing.Id.ToString(), purrConfig, createdBy);

            var package = new Package
            {
                Name = purrConfig.Name,
                Version = purrConfig.Version,
                Authors = JsonSerializer.Serialize(purrConfig.Authors),
                SupportedPlatforms = JsonSerializer.Serialize(purrConfig.SupportedPlatforms),
                Description = purrConfig.Description,
                ReadmeUrl = purrConfig.ReadmeUrl,
                License = purrConfig.License,
                LicenseUrl = purrConfig.LicenseUrl,
                Keywords = JsonSerializer.Serialize(purrConfig.Keywords),
                Categories = JsonSerializer.Serialize(purrConfig.Categories),
                Git = purrConfig.Git,
                OwnerId = ownerId != null ? int.Parse(ownerId) : null,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                LastUpdated = DateTime.UtcNow.ToString("O"),
                IsActive = 1
            };
            _context.Packages.Add(package);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SavePackageAsync(PurrConfig purrConfig, string createdBy)
            => await SavePackageAsync(purrConfig, createdBy, null);


        public async Task<bool> UpdatePackageAsync(string id, PurrConfig purrConfig, string? updatedBy = null)
        {
            var package = await _context.Packages.FindAsync(int.Parse(id));
            if (package == null) return false;
            
            package.Version = purrConfig.Version;
            package.Authors = JsonSerializer.Serialize(purrConfig.Authors);
            package.Categories = JsonSerializer.Serialize(purrConfig.Categories);
            package.Keywords = JsonSerializer.Serialize(purrConfig.Keywords);
            package.LastUpdated = DateTime.UtcNow.ToString("O");
            
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePackageAsync(string id)
        {
            var p = await _context.Packages.FindAsync(int.Parse(id));
            if (p == null) return false;
            _context.Packages.Remove(p);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> TogglePackageStatusAsync(string id)
        {
            var p = await _context.Packages.FindAsync(int.Parse(id));
            if (p == null) return false;
            p.IsActive = p.IsActive == 1 ? 0 : 1;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<SearchResult> SearchPackagesAsync(string? query = null, string? sort = null, int page = 1, int pageSize = 20)
        {
            var all = await _context.Packages.Where(p => p.IsActive == 1).ToListAsync();
            return new SearchResult { Packages = all, TotalCount = all.Count };
        }

        public async Task<PackageListResponse> GetPackageListAsync(string? sort = null, string? search = null, bool includeDetails = false)
        {
            var all = await _context.Packages.ToListAsync();
            return new PackageListResponse { PackageCount = all.Count, Packages = all.Select(p => p.Name).ToList() };
        }

        public async Task<List<Package>> GetPackagesByTagAsync(string tag)
        {
            var all = await _context.Packages.Where(p => p.IsActive == 1).ToListAsync();
            return all.Where(p => (JsonSerializer.Deserialize<List<string>>(p.Keywords) ?? new List<string>()).Contains(tag)).ToList();
        }

        public async Task<List<Package>> GetPackagesByAuthorAsync(string author)
        {
            var all = await _context.Packages.Where(p => p.IsActive == 1).ToListAsync();
            return all.Where(p => (JsonSerializer.Deserialize<List<string>>(p.Authors) ?? new List<string>()).Contains(author)).ToList();
        }

        public async Task<List<Package>> GetPackagesByCategoryAsync(string category)
        {
            var all = await _context.Packages.Where(p => p.IsActive == 1).ToListAsync();
            return all.Where(p => (JsonSerializer.Deserialize<List<string>>(p.Categories) ?? new List<string>()).Contains(category)).ToList();
        }

        public async Task<List<string>> GetPackageVersionsAsync(string packageName)
        {
            var p = await _context.Packages.FirstOrDefaultAsync(p => p.Name == packageName);
            return string.IsNullOrEmpty(p?.VersionHistory) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(p.VersionHistory) ?? new List<string>();
        }

        public async Task<PackageStatistics> GetStatisticsAsync() => new PackageStatistics();
        public async Task<bool> IncrementDownloadCountAsync(string id) => true;
        public async Task<bool> IncrementViewCountAsync(string id) => true;
        public async Task<bool> MarkPackageOutdatedAsync(string id, bool outdated) => true;
        public async Task<List<string>> GetPopularTagsAsync(int l) => new List<string>();
        public async Task<List<string>> GetPopularAuthorsAsync(int l) => new List<string>();
        public async Task<List<string>> GetPopularCategoriesAsync(int l) => new List<string>();
        public async Task<bool> InitializeDatabaseAsync() => true;
        public async Task<bool> ClearAllDataAsync() => true;
        public async Task<bool> ImportPackagesFromJsonAsync(string file) => true;
        public async Task<bool> ExportPackagesToJsonAsync(string file) => true;
        public async Task<int> GetPackageCountAsync() => await _context.Packages.CountAsync();
        public Task<bool> MigrateCategoriesAsync() => Task.FromResult(true);
        public async Task<List<PackageReview>> GetPackageReviewsAsync(string p) => new List<PackageReview>();
        public async Task<(bool success, string error)> AddPackageReviewAsync(string p, string? u, string rN, string? rA, int r, string t, string b) => (true, "");
        public async Task<bool> HasUserReviewedPackageAsync(string p, string u) => true;
        public async Task<bool> DeleteReviewAsync(string r, string? u, bool i) => true;
        public async Task<DependencyNode?> GetDependencyTreeAsync(string p, int m) => null;
    }
}
