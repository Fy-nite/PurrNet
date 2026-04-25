using MongoDB.Bson;
using MongoDB.Driver;
using Purrnet.Data;
using Purrnet.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Purrnet.Services
{
    public class PackageService : IPackageService
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<PackageService> _logger;
        private readonly static string _sanitizeRegex = @"[^\x20-\x7e]+";

        public PackageService(MongoDbContext context, ILogger<PackageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ─── CRUD ─────────────────────────────────────────────────────────────────

        public async Task<List<Package>> GetAllPackagesAsync()
        {
            try
            {
                return await _context.Packages
                    .Find(FilterDefinition<Package>.Empty)
                    .SortBy(p => p.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all packages");
                return new List<Package>();
            }
        }

        public async Task<Package?> GetPackageAsync(string packageName, string? version = null)
        {
            try
            {
                var filter = Builders<Package>.Filter.And(
                    Builders<Package>.Filter.Eq(p => p.Name, packageName),
                    Builders<Package>.Filter.Eq(p => p.IsActive, true));

                if (!string.IsNullOrEmpty(version))
                    filter &= Builders<Package>.Filter.Eq(p => p.Version, version);

                return await _context.Packages.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving package {PackageName}", Regex.Replace(packageName, _sanitizeRegex, ""));
                return null;
            }
        }

        public async Task<Package?> GetPackageByIdAsync(string id)
        {
            try
            {
                return await _context.Packages
                    .Find(p => p.Id == id)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving package by ID {PackageId}", id);
                return null;
            }
        }

        public async Task<bool> SavePackageAsync(PurrConfig purrConfig, string createdBy, string? ownerId = null)
        {
            try
            {
                // Check if package already exists
                var existing = await _context.Packages
                    .Find(p => p.Name == purrConfig.Name)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    _logger.LogInformation("Package {PackageName} already exists; updating", purrConfig.Name);
                    return await UpdatePackageAsync(existing.Id, purrConfig, createdBy);
                }

                // Validate ownerId
                if (!string.IsNullOrEmpty(ownerId))
                {
                    var owner = await _context.Users.Find(u => u.Id == ownerId).FirstOrDefaultAsync();
                    if (owner == null)
                    {
                        _logger.LogWarning("OwnerId {OwnerId} not found; clearing OwnerId", ownerId);
                        ownerId = null;
                    }
                }

                var package = new Package
                {
                    Name = purrConfig.Name,
                    Version = purrConfig.Version,
                    Authors = purrConfig.Authors ?? new List<string>(),
                    SupportedPlatforms = purrConfig.SupportedPlatforms ?? new List<string>(),
                    Description = purrConfig.Description ?? string.Empty,
                    ReadmeUrl = purrConfig.ReadmeUrl ?? string.Empty,
                    License = purrConfig.License ?? string.Empty,
                    LicenseUrl = purrConfig.LicenseUrl ?? string.Empty,
                    Keywords = purrConfig.Keywords ?? new List<string>(),
                    Categories = NormalizeCategories(purrConfig.Categories),
                    Homepage = purrConfig.Homepage ?? string.Empty,
                    IssueTracker = purrConfig.IssueTracker ?? string.Empty,
                    Git = purrConfig.Git,
                    Installer = purrConfig.Installer ?? string.Empty,
                    MainFile = purrConfig.MainFile,
                    Dependencies = purrConfig.Dependencies ?? new List<string>(),
                    IconUrl = purrConfig.IconUrl ?? string.Empty,
                    InstallCommand = $"Purr install {purrConfig.Name}",
                    Downloads = 0,
                    ViewCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    CreatedBy = createdBy,
                    OwnerId = ownerId,
                    IsActive = true,
                    ApprovalStatus = "Pending"
                };

                await _context.Packages.InsertOneAsync(package);
                await EnsureCategoriesAsync(package.Categories);

                _logger.LogInformation("Package {PackageName} saved by {CreatedBy}", purrConfig.Name, createdBy);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving package {PackageName}", purrConfig.Name);
                return false;
            }
        }

        public async Task<bool> SavePackageAsync(PurrConfig purrConfig, string createdBy)
            => await SavePackageAsync(purrConfig, createdBy, null);

        public async Task<bool> UpdatePackageAsync(string id, PurrConfig purrConfig, string? updatedBy = null)
        {
            try
            {
                var package = await _context.Packages.Find(p => p.Id == id).FirstOrDefaultAsync();
                if (package == null) return false;

                // Track version history
                if (!string.IsNullOrEmpty(package.Version) && package.Version != purrConfig.Version)
                {
                    var history = package.VersionHistory ?? new List<string>();
                    if (!history.Contains(package.Version))
                        history.Add(package.Version);

                    var update = Builders<Package>.Update
                        .Set(p => p.Version, purrConfig.Version)
                        .Set(p => p.Authors, purrConfig.Authors ?? new List<string>())
                        .Set(p => p.SupportedPlatforms, purrConfig.SupportedPlatforms ?? new List<string>())
                        .Set(p => p.Description, purrConfig.Description ?? string.Empty)
                        .Set(p => p.ReadmeUrl, purrConfig.ReadmeUrl ?? string.Empty)
                        .Set(p => p.License, purrConfig.License ?? string.Empty)
                        .Set(p => p.LicenseUrl, purrConfig.LicenseUrl ?? string.Empty)
                        .Set(p => p.Keywords, purrConfig.Keywords ?? new List<string>())
                        .Set(p => p.Categories, NormalizeCategories(purrConfig.Categories))
                        .Set(p => p.Homepage, purrConfig.Homepage ?? string.Empty)
                        .Set(p => p.IssueTracker, purrConfig.IssueTracker ?? string.Empty)
                        .Set(p => p.Git, purrConfig.Git)
                        .Set(p => p.Installer, purrConfig.Installer ?? string.Empty)
                        .Set(p => p.MainFile, purrConfig.MainFile)
                        .Set(p => p.Dependencies, purrConfig.Dependencies ?? new List<string>())
                        .Set(p => p.IconUrl, purrConfig.IconUrl ?? string.Empty)
                        .Set(p => p.VersionHistory, history)
                        .Set(p => p.LastUpdated, DateTime.UtcNow)
                        .Set(p => p.UpdatedBy, updatedBy);

                    await _context.Packages.UpdateOneAsync(p => p.Id == id, update);
                }
                else
                {
                    var update = Builders<Package>.Update
                        .Set(p => p.Version, purrConfig.Version)
                        .Set(p => p.Authors, purrConfig.Authors ?? new List<string>())
                        .Set(p => p.SupportedPlatforms, purrConfig.SupportedPlatforms ?? new List<string>())
                        .Set(p => p.Description, purrConfig.Description ?? string.Empty)
                        .Set(p => p.ReadmeUrl, purrConfig.ReadmeUrl ?? string.Empty)
                        .Set(p => p.License, purrConfig.License ?? string.Empty)
                        .Set(p => p.LicenseUrl, purrConfig.LicenseUrl ?? string.Empty)
                        .Set(p => p.Keywords, purrConfig.Keywords ?? new List<string>())
                        .Set(p => p.Categories, NormalizeCategories(purrConfig.Categories))
                        .Set(p => p.Homepage, purrConfig.Homepage ?? string.Empty)
                        .Set(p => p.IssueTracker, purrConfig.IssueTracker ?? string.Empty)
                        .Set(p => p.Git, purrConfig.Git)
                        .Set(p => p.Installer, purrConfig.Installer ?? string.Empty)
                        .Set(p => p.MainFile, purrConfig.MainFile)
                        .Set(p => p.Dependencies, purrConfig.Dependencies ?? new List<string>())
                        .Set(p => p.IconUrl, purrConfig.IconUrl ?? string.Empty)
                        .Set(p => p.LastUpdated, DateTime.UtcNow)
                        .Set(p => p.UpdatedBy, updatedBy);

                    await _context.Packages.UpdateOneAsync(p => p.Id == id, update);
                }

                await EnsureCategoriesAsync(NormalizeCategories(purrConfig.Categories));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating package {PackageId}", id);
                return false;
            }
        }

        public async Task<bool> DeletePackageAsync(string id)
        {
            try
            {
                var result = await _context.Packages.DeleteOneAsync(p => p.Id == id);
                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting package {PackageId}", id);
                return false;
            }
        }

        public async Task<bool> TogglePackageStatusAsync(string id)
        {
            try
            {
                var package = await _context.Packages.Find(p => p.Id == id).FirstOrDefaultAsync();
                if (package == null) return false;

                var update = Builders<Package>.Update.Set(p => p.IsActive, !package.IsActive);
                await _context.Packages.UpdateOneAsync(p => p.Id == id, update);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling package status {PackageId}", id);
                return false;
            }
        }

        // ─── Search ───────────────────────────────────────────────────────────────

        public async Task<SearchResult> SearchPackagesAsync(string? query = null, string? sort = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var allActive = await _context.Packages
                    .Find(p => p.IsActive)
                    .ToListAsync();

                IEnumerable<Package> filtered = allActive;

                if (!string.IsNullOrEmpty(query))
                {
                    filtered = allActive.Where(p =>
                        p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (p.Authors?.Any(a => a.Contains(query, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                        (p.Keywords?.Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                        (p.Categories?.Any(c => c.Contains(query, StringComparison.OrdinalIgnoreCase)) ?? false));
                }

                IEnumerable<Package> sorted = sort?.ToLower() switch
                {
                    "mostdownloads" => filtered.OrderByDescending(p => p.Downloads),
                    "leastdownloads" => filtered.OrderBy(p => p.Downloads),
                    "recentlyupdated" => filtered.OrderByDescending(p => p.LastUpdated),
                    "recentlyuploaded" => filtered.OrderByDescending(p => p.CreatedAt),
                    "oldestupdated" => filtered.OrderBy(p => p.LastUpdated),
                    "oldestuploaded" => filtered.OrderBy(p => p.CreatedAt),
                    "mostviewed" => filtered.OrderByDescending(p => p.ViewCount),
                    "toprated" => filtered.OrderByDescending(p => p.Rating),
                    _ => filtered.OrderBy(p => p.Name)
                };

                var total = sorted.Count();
                var packages = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return new SearchResult { Packages = packages, TotalCount = total, Query = query ?? string.Empty };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching packages with query '{Query}'", Regex.Replace(query ?? string.Empty, _sanitizeRegex, ""));
                return new SearchResult { Packages = new List<Package>(), TotalCount = 0, Query = query ?? string.Empty };
            }
        }

        public async Task<PackageListResponse> GetPackageListAsync(string? sort = null, string? search = null, bool includeDetails = false)
        {
            var result = await SearchPackagesAsync(search, sort, 1, 1000);
            var response = new PackageListResponse
            {
                PackageCount = result.TotalCount,
                Packages = result.Packages.Select(p => p.Name).ToList()
            };

            if (includeDetails)
            {
                response.PackageDetails = result.Packages.Select(ToConfig).ToList();
            }

            return response;
        }

        public async Task<List<Package>> GetPackagesByTagAsync(string tag)
        {
            try
            {
                var filter = Builders<Package>.Filter.And(
                    Builders<Package>.Filter.Eq(p => p.IsActive, true),
                    Builders<Package>.Filter.AnyEq(p => p.Keywords, tag));

                return await _context.Packages
                    .Find(filter)
                    .SortByDescending(p => p.Downloads)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving packages by tag {Tag}", tag);
                return new List<Package>();
            }
        }

        public async Task<List<Package>> GetPackagesByAuthorAsync(string author)
        {
            try
            {
                var filter = Builders<Package>.Filter.And(
                    Builders<Package>.Filter.Eq(p => p.IsActive, true),
                    Builders<Package>.Filter.AnyEq(p => p.Authors, author));

                return await _context.Packages
                    .Find(filter)
                    .SortByDescending(p => p.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving packages by author {Author}", author);
                return new List<Package>();
            }
        }

        public async Task<List<Package>> GetPackagesByCategoryAsync(string category)
        {
            try
            {
                var filter = Builders<Package>.Filter.And(
                    Builders<Package>.Filter.Eq(p => p.IsActive, true),
                    Builders<Package>.Filter.AnyEq(p => p.Categories, category));

                return await _context.Packages
                    .Find(filter)
                    .SortByDescending(p => p.Downloads)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving packages by category {Category}", category);
                return new List<Package>();
            }
        }

        public async Task<List<string>> GetPackageVersionsAsync(string packageName)
        {
            var package = await _context.Packages
                .Find(p => p.Name == packageName && p.IsActive)
                .FirstOrDefaultAsync();

            if (package == null) return new List<string>();

            var versions = new List<string>(package.VersionHistory ?? new List<string>());
            if (!string.IsNullOrEmpty(package.Version))
            {
                versions.Remove(package.Version);
                versions.Insert(0, package.Version);
            }
            return versions;
        }

        // ─── Statistics ───────────────────────────────────────────────────────────

        public async Task<PackageStatistics> GetStatisticsAsync()
        {
            try
            {
                var packages = await _context.Packages.Find(FilterDefinition<Package>.Empty).ToListAsync();
                var active = packages.Where(p => p.IsActive).ToList();

                return new PackageStatistics
                {
                    TotalPackages = packages.Count,
                    ActivePackages = active.Count,
                    TotalDownloads = active.Sum(p => p.Downloads),
                    TotalViews = active.Sum(p => p.ViewCount),
                    PopularAuthors = packages
                        .SelectMany(p => p.Authors ?? new List<string>())
                        .Select(a => a.Trim())
                        .GroupBy(a => a)
                        .OrderByDescending(g => g.Count())
                        .Take(20)
                        .Select(g => g.Key)
                        .ToList(),
                    MostDownloaded = active.OrderByDescending(p => p.Downloads).Take(5).ToList(),
                    RecentlyAdded = active.OrderByDescending(p => p.CreatedAt).Take(5).ToList(),
                    LastUpdated = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving statistics");
                return new PackageStatistics { LastUpdated = DateTime.UtcNow };
            }
        }

        public async Task<bool> IncrementDownloadCountAsync(string packageId)
        {
            try
            {
                var update = Builders<Package>.Update.Inc(p => p.Downloads, 1);
                var result = await _context.Packages.UpdateOneAsync(p => p.Id == packageId, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing download count for {PackageId}", packageId);
                return false;
            }
        }

        public async Task<bool> IncrementViewCountAsync(string packageId)
        {
            try
            {
                var update = Builders<Package>.Update.Inc(p => p.ViewCount, 1);
                var result = await _context.Packages.UpdateOneAsync(p => p.Id == packageId, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing view count for {PackageId}", packageId);
                return false;
            }
        }

        public async Task<bool> MarkPackageOutdatedAsync(string packageId, bool outdated = true)
        {
            try
            {
                var update = Builders<Package>.Update.Set(p => p.IsOutdated, outdated);
                var result = await _context.Packages.UpdateOneAsync(p => p.Id == packageId, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking package {PackageId} outdated", packageId);
                return false;
            }
        }

        public async Task<List<string>> GetPopularTagsAsync(int limit = 10)
        {
            try
            {
                var packages = await _context.Packages.Find(p => p.IsActive).ToListAsync();
                return packages
                    .SelectMany(p => p.Keywords ?? new List<string>())
                    .GroupBy(t => t)
                    .OrderByDescending(g => g.Count())
                    .Take(limit)
                    .Select(g => g.Key)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving popular tags");
                return new List<string>();
            }
        }

        public async Task<List<string>> GetPopularAuthorsAsync(int limit = 10)
        {
            try
            {
                var packages = await _context.Packages.Find(p => p.IsActive).ToListAsync();
                return packages
                    .SelectMany(p => p.Authors ?? new List<string>())
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count())
                    .Take(limit)
                    .Select(g => g.Key)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving popular authors");
                return new List<string>();
            }
        }

        public async Task<List<string>> GetPopularCategoriesAsync(int limit = 10)
        {
            try
            {
                var packages = await _context.Packages.Find(p => p.IsActive).ToListAsync();
                return packages
                    .SelectMany(p => p.Categories ?? new List<string>())
                    .GroupBy(c => c)
                    .OrderByDescending(g => g.Count())
                    .Take(limit)
                    .Select(g => g.Key)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving popular categories");
                return new List<string>();
            }
        }

        // ─── Database Management ──────────────────────────────────────────────────

        public async Task<bool> InitializeDatabaseAsync()
        {
            try
            {
                await _context.SeedDefaultCategoriesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database");
                return false;
            }
        }

        public async Task<bool> ClearAllDataAsync()
        {
            try
            {
                await _context.Packages.DeleteManyAsync(FilterDefinition<Package>.Empty);
                _logger.LogInformation("Cleared all package data");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all package data");
                return false;
            }
        }

        public async Task<bool> ImportPackagesFromJsonAsync(string jsonFilePath)
        {
            try
            {
                if (!File.Exists(jsonFilePath))
                {
                    _logger.LogError("JSON file not found: {FilePath}", jsonFilePath);
                    return false;
                }

                var json = await File.ReadAllTextAsync(jsonFilePath);

                // Try rich export format first
                List<ExportPackageDto>? exported = null;
                try { exported = JsonSerializer.Deserialize<List<ExportPackageDto>>(json); }
                catch { }

                if (exported != null && exported.Any())
                {
                    int importedCount = 0;
                    foreach (var item in exported)
                    {
                        string? ownerId = null;
                        if (item.Owner != null)
                        {
                            User? user = null;
                            if (!string.IsNullOrWhiteSpace(item.Owner.GitHubId))
                                user = await _context.Users.Find(u => u.GitHubId == item.Owner.GitHubId).FirstOrDefaultAsync();
                            if (user == null && !string.IsNullOrWhiteSpace(item.Owner.Email))
                                user = await _context.Users.Find(u => u.Email == item.Owner.Email).FirstOrDefaultAsync();
                            if (user == null && !string.IsNullOrWhiteSpace(item.Owner.Username))
                                user = await _context.Users.Find(u => u.Username == item.Owner.Username).FirstOrDefaultAsync();

                            if (user == null)
                            {
                                user = new User
                                {
                                    GitHubId = item.Owner.GitHubId ?? string.Empty,
                                    Username = item.Owner.Username ?? item.Owner.GitHubId ?? "unknown",
                                    Email = item.Owner.Email ?? string.Empty,
                                    AvatarUrl = string.Empty,
                                    CreatedAt = DateTime.UtcNow,
                                    LastLoginAt = DateTime.UtcNow,
                                    IsAdmin = false
                                };
                                await _context.Users.InsertOneAsync(user);
                            }
                            ownerId = user.Id;
                        }

                        if (await SavePackageAsync(item.PurrConfig, "import", ownerId))
                            importedCount++;
                    }

                    _logger.LogInformation("Imported {ImportedCount}/{Total} packages from {FilePath}", importedCount, exported.Count, jsonFilePath);
                    return true;
                }

                // Fallback: legacy PurrConfig array
                var configs = JsonSerializer.Deserialize<List<PurrConfig>>(json);
                if (configs == null || !configs.Any())
                {
                    _logger.LogWarning("No packages in {FilePath}", jsonFilePath);
                    return true;
                }

                int legacy = 0;
                foreach (var c in configs)
                    if (await SavePackageAsync(c, "import")) legacy++;

                _logger.LogInformation("Imported {Count}/{Total} packages from {FilePath}", legacy, configs.Count, jsonFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing packages from {FilePath}", jsonFilePath);
                return false;
            }
        }

        public async Task<bool> ExportPackagesToJsonAsync(string jsonFilePath)
        {
            try
            {
                var packages = await _context.Packages.Find(FilterDefinition<Package>.Empty).ToListAsync();

                // Bulk fetch owners
                var ownerIds = packages
                    .Where(p => !string.IsNullOrEmpty(p.OwnerId))
                    .Select(p => p.OwnerId!)
                    .Distinct()
                    .ToList();

                var ownerFilter = Builders<User>.Filter.In(u => u.Id, ownerIds);
                var owners = ownerIds.Any()
                    ? (await _context.Users.Find(ownerFilter).ToListAsync())
                        .ToDictionary(u => u.Id)
                    : new Dictionary<string, User>();

                var exportList = packages.Select(p =>
                {
                    owners.TryGetValue(p.OwnerId ?? string.Empty, out var owner);
                    return new ExportPackageDto
                    {
                        PackageId = p.Id,
                        PurrConfig = ToConfig(p),
                        Owner = owner == null ? null : new OwnerInfo
                        {
                            Id = owner.Id,
                            GitHubId = owner.GitHubId,
                            Username = owner.Username,
                            Email = owner.Email
                        }
                    };
                }).ToList();

                var json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(jsonFilePath, json);
                _logger.LogInformation("Exported {Count} packages to {FilePath}", exportList.Count, jsonFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting packages to {FilePath}", jsonFilePath);
                return false;
            }
        }

        public async Task<int> GetPackageCountAsync()
        {
            try
            {
                return (int)await _context.Packages.CountDocumentsAsync(p => p.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving package count");
                return 0;
            }
        }

        public Task<bool> MigrateCategoriesAsync()
        {
            // No relational migration needed for MongoDB; categories are embedded as strings
            return Task.FromResult(true);
        }

        // ─── Reviews ──────────────────────────────────────────────────────────────

        public async Task<List<PackageReview>> GetPackageReviewsAsync(string packageName)
        {
            var package = await _context.Packages
                .Find(p => p.Name == packageName && p.IsActive)
                .FirstOrDefaultAsync();

            if (package == null) return new List<PackageReview>();

            return await _context.PackageReviews
                .Find(r => r.PackageId == package.Id)
                .SortByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<(bool success, string error)> AddPackageReviewAsync(
            string packageName, string? userId, string reviewerName,
            string? reviewerAvatarUrl, int rating, string title, string body)
        {
            try
            {
                var package = await _context.Packages
                    .Find(p => p.Name == packageName && p.IsActive)
                    .FirstOrDefaultAsync();

                if (package == null)
                    return (false, "Package not found.");

                if (rating < 1 || rating > 5)
                    return (false, "Rating must be between 1 and 5.");

                // One review per logged-in user
                if (!string.IsNullOrEmpty(userId))
                {
                    var exists = await _context.PackageReviews
                        .Find(r => r.PackageId == package.Id && r.UserId == userId)
                        .AnyAsync();
                    if (exists)
                        return (false, "You have already reviewed this package.");
                }

                var review = new PackageReview
                {
                    PackageId = package.Id,
                    UserId = userId,
                    Rating = rating,
                    Title = title.Trim(),
                    Body = body.Trim(),
                    ReviewerName = reviewerName,
                    ReviewerAvatarUrl = reviewerAvatarUrl,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.PackageReviews.InsertOneAsync(review);
                await RecalculateRatingAsync(package.Id);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding review for {PackageName}", Regex.Replace(packageName, _sanitizeRegex, ""));
                return (false, "An error occurred while saving the review.");
            }
        }

        public async Task<bool> HasUserReviewedPackageAsync(string packageName, string userId)
        {
            var package = await _context.Packages
                .Find(p => p.Name == packageName && p.IsActive)
                .FirstOrDefaultAsync();

            if (package == null) return false;

            return await _context.PackageReviews
                .Find(r => r.PackageId == package.Id && r.UserId == userId)
                .AnyAsync();
        }

        public async Task<bool> DeleteReviewAsync(string reviewId, string? requestingUserId, bool isAdmin)
        {
            try
            {
                var review = await _context.PackageReviews
                    .Find(r => r.Id == reviewId)
                    .FirstOrDefaultAsync();

                if (review == null) return false;
                if (!isAdmin && review.UserId != requestingUserId) return false;

                await _context.PackageReviews.DeleteOneAsync(r => r.Id == reviewId);
                await RecalculateRatingAsync(review.PackageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting review {ReviewId}", reviewId);
                return false;
            }
        }

        // ─── Dependency Tree ──────────────────────────────────────────────────────

        public async Task<DependencyNode?> GetDependencyTreeAsync(string packageName, int maxDepth = 3)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return await BuildNodeAsync(packageName, maxDepth, visited);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private async Task<DependencyNode?> BuildNodeAsync(string packageName, int depth, HashSet<string> visited)
        {
            var package = await _context.Packages
                .Find(p => p.Name == packageName && p.IsActive)
                .FirstOrDefaultAsync();

            if (package == null)
                return new DependencyNode { Name = packageName, Resolved = false };

            var node = new DependencyNode
            {
                Name = package.Name,
                Version = package.Version,
                Description = package.Description,
                Resolved = true
            };

            if (depth <= 0 || visited.Contains(packageName)) return node;
            visited.Add(packageName);

            foreach (var dep in package.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dep)) continue;
                var child = await BuildNodeAsync(dep.Trim(), depth - 1, visited);
                if (child != null) node.Dependencies.Add(child);
            }

            return node;
        }

        private async Task RecalculateRatingAsync(string packageId)
        {
            var ratings = await _context.PackageReviews
                .Find(r => r.PackageId == packageId)
                .Project(r => r.Rating)
                .ToListAsync();

            var avg = ratings.Count > 0 ? ratings.Average() : 0.0;
            var update = Builders<Package>.Update
                .Set(p => p.Rating, avg)
                .Set(p => p.RatingCount, ratings.Count);

            await _context.Packages.UpdateOneAsync(p => p.Id == packageId, update);
        }

        private static List<string> NormalizeCategories(List<string>? input) =>
            input?.Where(c => !string.IsNullOrWhiteSpace(c))
                 .Select(c => c.Trim())
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .ToList() ?? new List<string>();

        private async Task EnsureCategoriesAsync(List<string> names)
        {
            foreach (var name in names)
            {
                var exists = await _context.Categories
                    .Find(c => c.Name == name)
                    .AnyAsync();

                if (!exists)
                    await _context.Categories.InsertOneAsync(new Category { Name = name });
            }
        }

        private static PurrConfig ToConfig(Package p) => new()
        {
            Name = p.Name,
            Version = p.Version,
            Authors = p.Authors,
            SupportedPlatforms = p.SupportedPlatforms,
            Description = p.Description,
            ReadmeUrl = p.ReadmeUrl,
            License = p.License,
            LicenseUrl = p.LicenseUrl,
            Keywords = p.Keywords,
            Categories = p.Categories,
            Homepage = p.Homepage,
            IssueTracker = p.IssueTracker,
            Git = p.Git,
            Installer = p.Installer,
            Dependencies = p.Dependencies,
            MainFile = p.MainFile,
            IconUrl = p.IconUrl
        };
    }
}
