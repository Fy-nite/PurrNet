using Purrnet.Models;
using Purrnet.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Purrnet.Services
{
    public class PackageService : IPackageService
    {
        private readonly PurrDbContext _context;
        private readonly ILogger<PackageService> _logger;

        public PackageService(PurrDbContext context, ILogger<PackageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Package>> GetAllPackagesAsync()
        {
            try
            {
                return await _context.Packages
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
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
                var query = _context.Packages.Where(p => p.Name == packageName && p.IsActive);

                if (!string.IsNullOrEmpty(version))
                {
                    query = query.Where(p => p.Version == version);
                }

                return await query.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving package {PackageName}", packageName);
                return null;
            }
        }

        public async Task<Package?> GetPackageByIdAsync(int id)
        {
            try
            {
                return await _context.Packages.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving package by ID {PackageId}", id);
                return null;
            }
        }

        public async Task<bool> SavePackageAsync(PurrConfig PurrConfig, string createdBy, int? ownerId = null)
        {
            try
            {
                // Check if package already exists
                var existingPackage = await _context.Packages
                    .FirstOrDefaultAsync(p => p.Name == PurrConfig.Name);

                if (existingPackage != null)
                {
                    _logger.LogInformation("Package {PackageName} already exists; updating existing record", PurrConfig.Name);
                    // Delegate to update flow so version history is recorded and fields are refreshed
                    return await UpdatePackageAsync(existingPackage.Id, PurrConfig, createdBy);
                }

                // Validate ownerId points to an existing user to avoid FK failures
                int? validOwnerId = ownerId;
                if (ownerId.HasValue)
                {
                    var owner = await _context.Users.FindAsync(ownerId.Value);
                    if (owner == null)
                    {
                        _logger.LogWarning("OwnerId {OwnerId} not found for package {PackageName}; clearing OwnerId", ownerId, PurrConfig.Name);
                        validOwnerId = null;
                    }
                }

                var package = new Package
                {
                    Name = PurrConfig.Name,
                    Version = PurrConfig.Version,
                    Authors = PurrConfig.Authors ?? new List<string>(),
                    SupportedPlatforms = PurrConfig.SupportedPlatforms ?? new List<string>(),
                    Description = PurrConfig.Description ?? string.Empty,
                    ReadmeUrl = PurrConfig.ReadmeUrl ?? string.Empty,
                    License = PurrConfig.License ?? string.Empty,
                    LicenseUrl = PurrConfig.LicenseUrl ?? string.Empty,
                    Keywords = PurrConfig.Keywords ?? new List<string>(),
                    Homepage = PurrConfig.Homepage ?? string.Empty,
                    IssueTracker = PurrConfig.IssueTracker ?? string.Empty,
                    Git = PurrConfig.Git,
                    Installer = PurrConfig.Installer ?? string.Empty,
                    MainFile = PurrConfig.MainFile,
                    Dependencies = PurrConfig.Dependencies ?? new List<string>(),
                    IconUrl = PurrConfig.IconUrl ?? string.Empty,
                    InstallCommand = $"Purr install {PurrConfig.Name}",
                    Downloads = 0,
                    ViewCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    CreatedBy = createdBy,
                    OwnerId = validOwnerId,
                    IsActive = true,
                    ApprovalStatus = "Pending"
                };

                // Handle categories: ensure Category entities exist and link them
                if (PurrConfig.Categories != null && PurrConfig.Categories.Any())
                {
                    var categoryNames = PurrConfig.Categories
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var existing = await _context.Categories
                        .Where(c => categoryNames.Contains(c.Name))
                        .ToListAsync();

                    var missing = categoryNames
                        .Except(existing.Select(e => e.Name), StringComparer.OrdinalIgnoreCase)
                        .Select(n => new Category { Name = n })
                        .ToList();

                    if (missing.Any())
                    {
                        _context.Categories.AddRange(missing);
                    }

                    package.CategoryEntities = existing.Concat(missing).ToList();
                    // Also keep legacy string list for compatibility
                    package.Categories = categoryNames;
                }

                _context.Packages.Add(package);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Package {PackageName} saved successfully by {CreatedBy} (Owner ID: {OwnerId})", 
                    PurrConfig.Name, createdBy, ownerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving package {PackageName}", PurrConfig.Name);
                return false;
            }
        }

        // Overload for backward compatibility
        public async Task<bool> SavePackageAsync(PurrConfig PurrConfig, string createdBy)
        {
            return await SavePackageAsync(PurrConfig, createdBy, null);
        }

        public async Task<bool> UpdatePackageAsync(int id, PurrConfig PurrConfig, string? updatedBy = null)
        {
            try
            {
                var package = await _context.Packages.FindAsync(id);
                if (package == null)
                {
                    return false;
                }

                // Record the current version in history before updating
                if (!string.IsNullOrEmpty(package.Version) && package.Version != PurrConfig.Version)
                {
                    var history = package.VersionHistory ?? new List<string>();
                    if (!history.Contains(package.Version))
                    {
                        history.Add(package.Version);
                        package.VersionHistory = history;
                    }
                }

                package.Version = PurrConfig.Version;
                package.Authors = PurrConfig.Authors ?? new List<string>();
                package.SupportedPlatforms = PurrConfig.SupportedPlatforms ?? new List<string>();
                package.Description = PurrConfig.Description ?? string.Empty;
                package.ReadmeUrl = PurrConfig.ReadmeUrl ?? string.Empty;
                package.License = PurrConfig.License ?? string.Empty;
                package.LicenseUrl = PurrConfig.LicenseUrl ?? string.Empty;
                package.Keywords = PurrConfig.Keywords ?? new List<string>();
                package.Homepage = PurrConfig.Homepage ?? string.Empty;
                package.IssueTracker = PurrConfig.IssueTracker ?? string.Empty;
                package.Git = PurrConfig.Git;
                package.Installer = PurrConfig.Installer ?? string.Empty;
                package.MainFile = PurrConfig.MainFile;
                package.Dependencies = PurrConfig.Dependencies ?? new List<string>();
                package.IconUrl = PurrConfig.IconUrl ?? string.Empty;
                // Update categories relationally and as legacy list
                if (PurrConfig.Categories != null)
                {
                    var categoryNames = PurrConfig.Categories
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var existing = await _context.Categories
                        .Where(c => categoryNames.Contains(c.Name))
                        .ToListAsync();

                    var missing = categoryNames
                        .Except(existing.Select(e => e.Name), StringComparer.OrdinalIgnoreCase)
                        .Select(n => new Category { Name = n })
                        .ToList();

                    if (missing.Any()) _context.Categories.AddRange(missing);

                    // Reload package's category collection
                    await _context.Entry(package).Collection(p => p.CategoryEntities).LoadAsync();
                    package.CategoryEntities.Clear();
                    package.CategoryEntities.AddRange(existing.Concat(missing));
                    package.Categories = categoryNames;
                }
                package.LastUpdated = DateTime.UtcNow;
                package.UpdatedBy = updatedBy;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating package {PackageId}", id);
                return false;
            }
        }

        public async Task<bool> DeletePackageAsync(int id)
        {
            try
            {
                var package = await _context.Packages.FindAsync(id);
                if (package == null)
                {
                    return false;
                }

                _context.Packages.Remove(package);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting package {PackageId}", id);
                return false;
            }
        }

        public async Task<List<string>> GetPackageVersionsAsync(string packageName)
        {
            var package = await _context.Packages
                .Where(p => p.Name == packageName && p.IsActive)
                .FirstOrDefaultAsync();

            if (package == null)
                return new List<string>();

            var versions = new List<string>();
            if (package.VersionHistory != null)
                versions.AddRange(package.VersionHistory);

            // Add the current version at the top
            if (!string.IsNullOrEmpty(package.Version) && !versions.Contains(package.Version))
                versions.Insert(0, package.Version);
            else if (!string.IsNullOrEmpty(package.Version))
            {
                versions.Remove(package.Version);
                versions.Insert(0, package.Version);
            }

            return versions;
        }

        public async Task<bool> TogglePackageStatusAsync(int id)
        {
            try
            {
                var package = await _context.Packages.FindAsync(id);
                if (package == null)
                {
                    return false;
                }

                package.IsActive = !package.IsActive;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling package status {PackageId}", id);
                return false;
            }
        }

        public async Task<SearchResult> SearchPackagesAsync(string? query = null, string? sort = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var queryable = _context.Packages.Where(p => p.IsActive);

                List<Package> filteredPackages;

                // Apply search filter (client-side for case-insensitive search)
                if (!string.IsNullOrEmpty(query))
                {
                    var searchTerm = query;
                    filteredPackages = await queryable.ToListAsync();
                    filteredPackages = filteredPackages.Where(p =>
                        (!string.IsNullOrEmpty(p.Name) && p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(p.Description) && p.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (p.Authors != null && p.Authors.Any(a => !string.IsNullOrEmpty(a) && a.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))) ||
                        (p.Keywords != null && p.Keywords.Any(k => !string.IsNullOrEmpty(k) && k.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))) ||
                        (p.Categories != null && p.Categories.Any(c => !string.IsNullOrEmpty(c) && c.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                    ).ToList();
                }
                else
                {
                    filteredPackages = await queryable.ToListAsync();
                }

                // Apply sorting (client-side)
                IEnumerable<Package> sortedPackages = sort?.ToLower() switch
                {
                    "mostdownloads" => filteredPackages.OrderByDescending(p => p.Downloads),
                    "leastdownloads" => filteredPackages.OrderBy(p => p.Downloads),
                    "recentlyupdated" => filteredPackages.OrderByDescending(p => p.LastUpdated),
                    "recentlyuploaded" => filteredPackages.OrderByDescending(p => p.CreatedAt),
                    "oldestupdated" => filteredPackages.OrderBy(p => p.LastUpdated),
                    "oldestuploaded" => filteredPackages.OrderBy(p => p.CreatedAt),
                    "mostviewed" => filteredPackages.OrderByDescending(p => p.ViewCount),
                    "toprated" => filteredPackages.OrderByDescending(p => p.Rating),
                    _ => filteredPackages.OrderBy(p => p.Name)
                };

                var totalCount = sortedPackages.Count();
                var packages = sortedPackages
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new SearchResult
                {
                    Packages = packages,
                    TotalCount = totalCount,
                    Query = query ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching packages with query '{Query}'", query);
                return new SearchResult { Packages = new List<Package>(), TotalCount = 0, Query = query ?? string.Empty };
            }
        }

        public async Task<PackageListResponse> GetPackageListAsync(string? sort = null, string? search = null, bool includeDetails = false)
        {
            var searchResult = await SearchPackagesAsync(search, sort, 1, 1000);

            var response = new PackageListResponse
            {
                PackageCount = searchResult.TotalCount,
                Packages = searchResult.Packages.Select(p => p.Name).ToList()
            };

            if (includeDetails)
            {
                response.PackageDetails = searchResult.Packages.Select(p => new PurrConfig
                {
                    Name = p.Name,
                    Version = p.Version,
                    Authors = p.Authors,
                    Description = p.Description,
                    Keywords = p.Keywords,
                    Homepage = p.Homepage,
                    IssueTracker = p.IssueTracker,
                    Git = p.Git,
                    Installer = p.Installer,
                    Dependencies = p.Dependencies,
                    MainFile = p.MainFile,
                    IconUrl = p.IconUrl
                }).ToList();
            }

            return response;
        }

        public async Task<bool> MarkPackageOutdatedAsync(int packageId, bool outdated = true)
        {
            try
            {
                var pkg = await _context.Packages.FindAsync(packageId);
                if (pkg == null) return false;
                pkg.IsOutdated = outdated;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Package {PackageName} (ID {PackageId}) outdated flag set to {Outdated}", pkg.Name, packageId, outdated);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking package {PackageId} outdated", packageId);
                return false;
            }
        }

        public async Task<PackageStatistics> GetStatisticsAsync()
        {
            try
            {
                var packages = await _context.Packages.ToListAsync();
                var activePackages = packages.Where(p => p.IsActive).ToList();

                return new PackageStatistics
                {
                    TotalPackages = packages.Count,
                    ActivePackages = activePackages.Count,
                    TotalDownloads = activePackages.Sum(p => p.Downloads),
                    TotalViews = activePackages.Sum(p => p.ViewCount),
                    PopularAuthors = packages.Where(p => p.Authors != null && p.Authors.Any())
                        .SelectMany(p => p.Authors.Select(a => a.Trim()))
                        .GroupBy(author => author)
                        .OrderByDescending(g => g.Count())
                        .Take(20)
                        .Select(g => g.Key)
                        .ToList(),
                    MostDownloaded = activePackages.OrderByDescending(p => p.Downloads).Take(5).ToList(),
                    RecentlyAdded = activePackages.OrderByDescending(p => p.CreatedAt).Take(5).ToList(),
                    LastUpdated = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving package statistics");
                return new PackageStatistics { LastUpdated = DateTime.UtcNow };
            }
        }

        public async Task<bool> IncrementDownloadCountAsync(int packageId)
        {
            try
            {
                var package = await _context.Packages.FindAsync(packageId);
                if (package != null)
                {
                    package.Downloads++;
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing download count for package {PackageId}", packageId);
                return false;
            }
        }

        public async Task<bool> IncrementViewCountAsync(int packageId)
        {
            try
            {
                var package = await _context.Packages.FindAsync(packageId);
                if (package != null)
                {
                    package.ViewCount++;
                    await _context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing view count for package {PackageId}", packageId);
                return false;
            }
        }

        public async Task<bool> InitializeDatabaseAsync()
        {
            try
            {
                await _context.Database.EnsureCreatedAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing database");
                return false;
            }
        }

        

        public async Task<List<Package>> GetPackagesByTagAsync(string tag)
        {
            try
            {
                return await _context.Packages
                    .Where(p => p.IsActive && p.Keywords.Contains(tag))
                    .OrderByDescending(p => p.Downloads)
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
                return await _context.Packages
                    .Where(p => p.IsActive && p.Authors.Contains(author))
                    .OrderByDescending(p => p.CreatedAt)
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
                return await _context.Packages
                    .Where(p => p.IsActive && p.CategoryEntities.Any(c => c.Name == category))
                    .OrderByDescending(p => p.Downloads)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving packages by category {Category}", category);
                return new List<Package>();
            }
        }

        public async Task<List<string>> GetPopularTagsAsync(int limit = 10)
        {
            try
            {
                var packages = await _context.Packages.Where(p => p.IsActive).ToListAsync();
                return packages
                    .SelectMany(p => p.Keywords)
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
                var packages = await _context.Packages.Where(p => p.IsActive).ToListAsync();
                return packages
                    .SelectMany(p => p.Authors)
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
                return await _context.Categories
                    .OrderByDescending(c => c.Packages.Count)
                    .Take(limit)
                    .Select(c => c.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving popular categories");
                return new List<string>();
            }
        }

        public async Task<bool> ClearAllDataAsync()
        {
            try
            {
                var packages = await _context.Packages.ToListAsync();
                _context.Packages.RemoveRange(packages);
                await _context.SaveChangesAsync();
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
                // Try to deserialize rich export format first (includes owner info)
                List<ExportPackageDto>? exported = null;
                try
                {
                    exported = JsonSerializer.Deserialize<List<ExportPackageDto>>(json);
                }
                catch { /* fall back below */ }

                if (exported != null && exported.Any())
                {
                    int importedCount = 0;
                    foreach (var item in exported)
                    {
                        int? ownerId = null;
                        if (item.Owner != null)
                        {
                            // Try to find existing user by GitHubId, then Email, then Username
                            User? user = null;
                            if (!string.IsNullOrWhiteSpace(item.Owner.GitHubId))
                            {
                                user = await _context.Users.FirstOrDefaultAsync(u => u.GitHubId == item.Owner.GitHubId);
                            }
                            if (user == null && !string.IsNullOrWhiteSpace(item.Owner.Email))
                            {
                                user = await _context.Users.FirstOrDefaultAsync(u => u.Email == item.Owner.Email);
                            }
                            if (user == null && !string.IsNullOrWhiteSpace(item.Owner.Username))
                            {
                                user = await _context.Users.FirstOrDefaultAsync(u => u.Username == item.Owner.Username);
                            }

                            if (user == null)
                            {
                                // Create a lightweight placeholder user so ownership can be tracked across instances
                                user = new User
                                {
                                    GitHubId = item.Owner.GitHubId ?? string.Empty,
                                    Username = item.Owner.Username ?? (item.Owner.GitHubId ?? "unknown"),
                                    Email = item.Owner.Email ?? string.Empty,
                                    AvatarUrl = string.Empty,
                                    CreatedAt = DateTime.UtcNow,
                                    LastLoginAt = DateTime.UtcNow,
                                    IsAdmin = false
                                };
                                _context.Users.Add(user);
                                await _context.SaveChangesAsync();
                            }

                            ownerId = user.Id;
                        }

                        if (await SavePackageAsync(item.PurrConfig, "import", ownerId))
                        {
                            importedCount++;
                        }
                    }

                    _logger.LogInformation("Imported {ImportedCount} out of {TotalCount} packages from {FilePath}", importedCount, exported.Count, jsonFilePath);
                    return true;
                }

                // Fallback to legacy simple PurrConfig array
                var PurrConfigs = JsonSerializer.Deserialize<List<PurrConfig>>(json);

                if (PurrConfigs == null || !PurrConfigs.Any())
                {
                    _logger.LogWarning("No packages found in JSON file: {FilePath}", jsonFilePath);
                    return true;
                }

                int legacyImported = 0;
                foreach (var PurrConfig in PurrConfigs)
                {
                    if (await SavePackageAsync(PurrConfig, "import"))
                    {
                        legacyImported++;
                    }
                }

                _logger.LogInformation("Imported {ImportedCount} out of {TotalCount} packages from {FilePath}", legacyImported, PurrConfigs.Count, jsonFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing packages from JSON file: {FilePath}", jsonFilePath);
                return false;
            }
        }

        public Task<bool> ExportPackagesToJsonAsync(string jsonFilePath)
        {
            try
            {
                var packages = _context.Packages
                    .Include(p => p.Owner)
                    .ToList();

                var exportList = packages.Select(p => new ExportPackageDto
                {
                    PackageId = p.Id,
                    PurrConfig = new PurrConfig
                    {
                        Name = p.Name,
                        Version = p.Version,
                        Authors = p.Authors ?? new List<string>(),
                        SupportedPlatforms = p.SupportedPlatforms ?? new List<string>(),
                        Description = p.Description ?? string.Empty,
                        ReadmeUrl = p.ReadmeUrl ?? string.Empty,
                        License = p.License ?? string.Empty,
                        LicenseUrl = p.LicenseUrl ?? string.Empty,
                        Keywords = p.Keywords ?? new List<string>(),
                        Categories = p.Categories ?? new List<string>(),
                        Homepage = p.Homepage ?? string.Empty,
                        IssueTracker = p.IssueTracker ?? string.Empty,
                        Git = p.Git ?? string.Empty,
                        Installer = p.Installer ?? string.Empty,
                        Dependencies = p.Dependencies ?? new List<string>(),
                        IconUrl = p.IconUrl ?? string.Empty
                    },
                    Owner = p.Owner == null ? null : new OwnerInfo
                    {
                        Id = p.Owner.Id,
                        GitHubId = p.Owner.GitHubId,
                        Username = p.Owner.Username,
                        Email = p.Owner.Email
                    }
                }).ToList();

                var json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonFilePath, json);
                _logger.LogInformation("Exported {Count} packages to {FilePath}", exportList.Count, jsonFilePath);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting packages to JSON file: {FilePath}", jsonFilePath);
                return Task.FromResult(false);
            }
        }

        public async Task<int> GetPackageCountAsync()
        {
            try
            {
                return await _context.Packages.CountAsync(p => p.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving package count");
                return 0;
            }
        }

        // Migrate legacy string-based categories into relational Category entities
        public async Task<bool> MigrateCategoriesAsync()
        {
            try
            {
                var packages = await _context.Packages.ToListAsync();
                var allCategories = packages
                    .Where(p => p.Categories != null)
                    .SelectMany(p => p.Categories)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var existing = await _context.Categories.Where(c => allCategories.Contains(c.Name)).ToListAsync();
                var missing = allCategories.Except(existing.Select(e => e.Name), StringComparer.OrdinalIgnoreCase)
                    .Select(n => new Category { Name = n })
                    .ToList();

                if (missing.Any()) _context.Categories.AddRange(missing);

                await _context.SaveChangesAsync();

                var categoryLookup = await _context.Categories.ToListAsync();

                foreach (var pkg in packages)
                {
                    if (pkg.Categories == null || !pkg.Categories.Any()) continue;
                    await _context.Entry(pkg).Collection(p => p.CategoryEntities).LoadAsync();
                    pkg.CategoryEntities.Clear();
                    var names = pkg.Categories.Select(c => c.Trim());
                    var cats = categoryLookup.Where(c => names.Contains(c.Name)).ToList();
                    pkg.CategoryEntities.AddRange(cats);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating legacy categories");
                return false;
            }
        }

        // ─── Reviews ──────────────────────────────────────────────────────────────

        public async Task<List<PackageReview>> GetPackageReviewsAsync(string packageName)
        {
            return await _context.PackageReviews
                .Where(r => r.Package != null && r.Package.Name == packageName)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<(bool success, string error)> AddPackageReviewAsync(
            string packageName, int? userId, string reviewerName,
            string? reviewerAvatarUrl, int rating, string title, string body)
        {
            try
            {
                var package = await _context.Packages.FirstOrDefaultAsync(p => p.Name == packageName && p.IsActive);
                if (package == null)
                    return (false, "Package not found.");

                if (rating < 1 || rating > 5)
                    return (false, "Rating must be between 1 and 5.");

                // One review per logged-in user
                if (userId.HasValue)
                {
                    var exists = await _context.PackageReviews
                        .AnyAsync(r => r.PackageId == package.Id && r.UserId == userId.Value);
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

                _context.PackageReviews.Add(review);
                await _context.SaveChangesAsync();

                // Recalculate aggregate rating on the Package
                var allRatings = await _context.PackageReviews
                    .Where(r => r.PackageId == package.Id)
                    .Select(r => r.Rating)
                    .ToListAsync();

                package.Rating = allRatings.Count > 0 ? allRatings.Average() : 0;
                package.RatingCount = allRatings.Count;
                await _context.SaveChangesAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding review for {PackageName}", packageName);
                return (false, "An error occurred while saving the review.");
            }
        }

        public async Task<bool> HasUserReviewedPackageAsync(string packageName, int userId)
        {
            return await _context.PackageReviews
                .AnyAsync(r => r.Package != null && r.Package.Name == packageName && r.UserId == userId);
        }

        public async Task<bool> DeleteReviewAsync(int reviewId, int? requestingUserId, bool isAdmin)
        {
            try
            {
                var review = await _context.PackageReviews.Include(r => r.Package).FirstOrDefaultAsync(r => r.Id == reviewId);
                if (review == null) return false;
                if (!isAdmin && review.UserId != requestingUserId) return false;

                _context.PackageReviews.Remove(review);
                await _context.SaveChangesAsync();

                // Recalculate aggregate rating
                if (review.Package != null)
                {
                    var allRatings = await _context.PackageReviews
                        .Where(r => r.PackageId == review.PackageId)
                        .Select(r => r.Rating)
                        .ToListAsync();

                    review.Package.Rating = allRatings.Count > 0 ? allRatings.Average() : 0;
                    review.Package.RatingCount = allRatings.Count;
                    await _context.SaveChangesAsync();
                }

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

        private async Task<DependencyNode?> BuildNodeAsync(string packageName, int depth, HashSet<string> visited)
        {
            var package = await _context.Packages
                .FirstOrDefaultAsync(p => p.Name == packageName && p.IsActive);

            if (package == null)
                return new DependencyNode { Name = packageName, Resolved = false };

            var node = new DependencyNode
            {
                Name = package.Name,
                Version = package.Version,
                Description = package.Description,
                Resolved = true
            };

            if (depth <= 0 || visited.Contains(packageName))
                return node;

            visited.Add(packageName);

            foreach (var dep in package.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dep)) continue;
                var child = await BuildNodeAsync(dep.Trim(), depth - 1, visited);
                if (child != null) node.Dependencies.Add(child);
            }

            return node;
        }
    }
}