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
            if (!int.TryParse(id, out int intId)) return null;
            return await _context.Packages.FirstOrDefaultAsync(p => p.Id == intId);
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
                Homepage = purrConfig.Homepage,
                IssueTracker = purrConfig.IssueTracker,
                Git = purrConfig.Git,
                Installer = purrConfig.Installer,
                Dependencies = JsonSerializer.Serialize(purrConfig.Dependencies),
                IconUrl = purrConfig.IconUrl,
                MainFile = purrConfig.MainFile,
                InstallCommand = $"purr install {purrConfig.Name}",
                OwnerId = ownerId != null ? int.Parse(ownerId) : null,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                LastUpdated = DateTime.UtcNow.ToString("O"),
                IsActive = 1,
                CreatedBy = createdBy,
                UpdatedBy = createdBy
            };
            _context.Packages.Add(package);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SavePackageAsync(PurrConfig purrConfig, string createdBy)
            => await SavePackageAsync(purrConfig, createdBy, null);


        public async Task<bool> UpdatePackageAsync(string id, PurrConfig purrConfig, string? updatedBy = null)
        {
            if (!int.TryParse(id, out int intId)) return false;
            var package = await _context.Packages.FindAsync(intId);
            if (package == null) return false;
            
            package.Version = purrConfig.Version;
            package.Authors = JsonSerializer.Serialize(purrConfig.Authors);
            package.SupportedPlatforms = JsonSerializer.Serialize(purrConfig.SupportedPlatforms);
            package.Description = purrConfig.Description;
            package.ReadmeUrl = purrConfig.ReadmeUrl;
            package.License = purrConfig.License;
            package.LicenseUrl = purrConfig.LicenseUrl;
            package.Categories = JsonSerializer.Serialize(purrConfig.Categories);
            package.Keywords = JsonSerializer.Serialize(purrConfig.Keywords);
            package.Homepage = purrConfig.Homepage;
            package.IssueTracker = purrConfig.IssueTracker;
            package.Git = purrConfig.Git;
            package.Installer = purrConfig.Installer;
            package.Dependencies = JsonSerializer.Serialize(purrConfig.Dependencies);
            package.IconUrl = purrConfig.IconUrl;
            package.MainFile = string.IsNullOrWhiteSpace(purrConfig.MainFile) ? package.MainFile : purrConfig.MainFile;
            package.LastUpdated = DateTime.UtcNow.ToString("O");
            package.UpdatedBy = updatedBy;
            // Keep InstallCommand in sync with name
            package.InstallCommand = $"purr install {package.Name}";
            
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePackageAsync(string id)
        {
            if (!int.TryParse(id, out int intId)) return false;
            var p = await _context.Packages.FindAsync(intId);
            if (p == null) return false;
            _context.Packages.Remove(p);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> TogglePackageStatusAsync(string id)
        {
            if (!int.TryParse(id, out int intId)) return false;
            var p = await _context.Packages.FindAsync(intId);
            if (p == null) return false;
            p.IsActive = p.IsActive == 1 ? 0 : 1;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<SearchResult> SearchPackagesAsync(string? query = null, string? sort = null, int page = 1, int pageSize = 20)
        {
            var dbQuery = _context.Packages.Where(p => p.IsActive == 1);
            if (!string.IsNullOrEmpty(query))
            {
                dbQuery = dbQuery.Where(p => p.Name.Contains(query) || p.Description.Contains(query));
            }
            var total = await dbQuery.CountAsync();
            var items = await dbQuery.OrderBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new SearchResult { Packages = items, TotalCount = total };
        }

        public async Task<PackageListResponse> GetPackageListAsync(string? sort = null, string? search = null, bool includeDetails = false)
        {
            var dbQuery = _context.Packages.AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                dbQuery = dbQuery.Where(p => p.Name.Contains(search));
            }
            var all = await dbQuery.ToListAsync();
            return new PackageListResponse { PackageCount = all.Count, Packages = all.Select(p => p.Name).ToList() };
        }

        public async Task<List<Package>> GetPackagesByTagAsync(string tag)
        {
            var all = await _context.Packages.Where(p => p.IsActive == 1).ToListAsync();
            return all.Where(p => p.KeywordsList.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        public async Task<List<Package>> GetPackagesByAuthorAsync(string author)
        {
            var all = await _context.Packages.Where(p => p.IsActive == 1).ToListAsync();
            return all.Where(p => p.AuthorsList.Contains(author, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        public async Task<List<Package>> GetPackagesByCategoryAsync(string category)
        {
            var all = await _context.Packages.Where(p => p.IsActive == 1).ToListAsync();
            return all.Where(p => p.CategoriesList.Contains(category, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        public async Task<List<string>> GetPackageVersionsAsync(string packageName)
        {
            var p = await _context.Packages.FirstOrDefaultAsync(p => p.Name == packageName);
            if (p == null) return new List<string>();
            var versions = p.VersionHistoryList;
            if (!versions.Contains(p.Version)) versions.Insert(0, p.Version);
            return versions;
        }

        public async Task<PackageStatistics> GetStatisticsAsync()
        {
            var packages = await _context.Packages.ToListAsync();
            var active = packages.Where(p => p.IsActive == 1).ToList();
            return new PackageStatistics
            {
                TotalPackages = packages.Count,
                ActivePackages = active.Count,
                TotalDownloads = active.Sum(p => p.Downloads),
                TotalViews = active.Sum(p => p.ViewCount),
                MostDownloaded = active.OrderByDescending(p => p.Downloads).Take(5).ToList(),
                RecentlyAdded = active.OrderByDescending(p => p.CreatedAt).Take(5).ToList(),
                LastUpdated = DateTime.UtcNow
            };
        }

        public async Task<bool> IncrementDownloadCountAsync(string id)
        {
            if (!int.TryParse(id, out int intId)) return false;
            var p = await _context.Packages.FindAsync(intId);
            if (p == null) return false;
            p.Downloads++;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IncrementViewCountAsync(string id)
        {
            if (!int.TryParse(id, out int intId)) return false;
            var p = await _context.Packages.FindAsync(intId);
            if (p == null) return false;
            p.ViewCount++;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkPackageOutdatedAsync(string id, bool outdated)
        {
            if (!int.TryParse(id, out int intId)) return false;
            var p = await _context.Packages.FindAsync(intId);
            if (p == null) return false;
            p.IsOutdated = outdated ? 1 : 0;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<string>> GetPopularTagsAsync(int limit)
        {
            var packages = await _context.Packages.Where(p => p.IsActive == 1).ToListAsync();
            return packages.SelectMany(p => p.KeywordsList).GroupBy(t => t).OrderByDescending(g => g.Count()).Take(limit).Select(g => g.Key).ToList();
        }

        public async Task<List<string>> GetPopularAuthorsAsync(int limit)
        {
            var packages = await _context.Packages.Where(p => p.IsActive == 1).ToListAsync();
            return packages.SelectMany(p => p.AuthorsList).GroupBy(a => a).OrderByDescending(g => g.Count()).Take(limit).Select(g => g.Key).ToList();
        }

        public async Task<List<string>> GetPopularCategoriesAsync(int limit)
        {
            var packages = await _context.Packages.Where(p => p.IsActive == 1).ToListAsync();
            return packages.SelectMany(p => p.CategoriesList).GroupBy(c => c).OrderByDescending(g => g.Count()).Take(limit).Select(g => g.Key).ToList();
        }

        public async Task<bool> InitializeDatabaseAsync() => true;
        public async Task<bool> ClearAllDataAsync()
        {
            _context.Packages.RemoveRange(_context.Packages);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ImportPackagesFromJsonAsync(string file) => true;
        public async Task<bool> ExportPackagesToJsonAsync(string file) => true;
        public async Task<int> GetPackageCountAsync() => await _context.Packages.CountAsync();
        public Task<bool> MigrateCategoriesAsync() => Task.FromResult(true);

        public async Task<List<PackageReview>> GetPackageReviewsAsync(string packageName)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Name == packageName);
            if (package == null) return new List<PackageReview>();
            return await _context.PackageReviews.Where(r => r.PackageId == package.Id).OrderByDescending(r => r.CreatedAt).ToListAsync();
        }

        public async Task<(bool success, string error)> AddPackageReviewAsync(string packageName, string? userId, string reviewerName, string? reviewerAvatarUrl, int rating, string title, string body)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Name == packageName);
            if (package == null) return (false, "Package not found");

            int? intUserId = null;
            if (userId != null && int.TryParse(userId, out int parsedUserId)) intUserId = parsedUserId;

            var review = new PackageReview
            {
                PackageId = package.Id,
                UserId = intUserId,
                Rating = rating,
                Title = title,
                Body = body,
                ReviewerName = reviewerName,
                ReviewerAvatarUrl = reviewerAvatarUrl ?? string.Empty,
                CreatedAt = DateTime.UtcNow.ToString("O")
            };
            _context.PackageReviews.Add(review);
            await _context.SaveChangesAsync();
            await RecalculateRatingAsync(package.Id);
            return (true, "");
        }

        public async Task<bool> HasUserReviewedPackageAsync(string packageName, string userId)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Name == packageName);
            if (package == null || !int.TryParse(userId, out int intUserId)) return false;
            return await _context.PackageReviews.AnyAsync(r => r.PackageId == package.Id && r.UserId == intUserId);
        }

        public async Task<bool> DeleteReviewAsync(string reviewId, string? userId, bool isAdmin)
        {
            if (!int.TryParse(reviewId, out int intReviewId)) return false;
            var review = await _context.PackageReviews.FindAsync(intReviewId);
            if (review == null) return false;
            if (!isAdmin && (userId == null || !int.TryParse(userId, out int intUserId) || review.UserId != intUserId)) return false;
            
            int pkgId = review.PackageId;
            _context.PackageReviews.Remove(review);
            await _context.SaveChangesAsync();
            await RecalculateRatingAsync(pkgId);
            return true;
        }

        private async Task RecalculateRatingAsync(int packageId)
        {
            var ratings = await _context.PackageReviews.Where(r => r.PackageId == packageId).Select(r => r.Rating).ToListAsync();
            var package = await _context.Packages.FindAsync(packageId);
            if (package != null)
            {
                package.Rating = ratings.Count > 0 ? ratings.Average() : 0;
                package.RatingCount = ratings.Count;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<DependencyNode?> GetDependencyTreeAsync(string packageName, int maxDepth = 3)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return await BuildNodeAsync(packageName, maxDepth, visited);
        }

        private async Task<DependencyNode?> BuildNodeAsync(string packageName, int depth, HashSet<string> visited)
        {
            var package = await _context.Packages.FirstOrDefaultAsync(p => p.Name == packageName && p.IsActive == 1);
            if (package == null) return new DependencyNode { Name = packageName, Resolved = false };

            var node = new DependencyNode { Name = package.Name, Version = package.Version, Description = package.Description, Resolved = true };
            if (depth <= 0 || visited.Contains(packageName)) return node;
            visited.Add(packageName);

            foreach (var dep in package.DependenciesList)
            {
                if (string.IsNullOrWhiteSpace(dep)) continue;
                var child = await BuildNodeAsync(dep.Trim(), depth - 1, visited);
                if (child != null) node.Dependencies.Add(child);
            }
            return node;
        }
    }
}
