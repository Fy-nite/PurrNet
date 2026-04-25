using MongoDB.Driver;
using Purrnet.Data;
using Purrnet.Models;

namespace Purrnet.Services
{
    public class AdminService : IAdminService
    {
        private readonly MongoDbContext _context;
        private readonly ILogger<AdminService> _logger;

        public AdminService(MongoDbContext context, ILogger<AdminService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Package>> GetPendingPackagesAsync()
        {
            try
            {
                return await _context.Packages
                    .Find(p => p.ApprovalStatus == "Pending")
                    .SortBy(p => p.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending packages");
                return new List<Package>();
            }
        }

        public async Task<List<Package>> GetPackagesByStatusAsync(string status, string? search = null, string? sortBy = null)
        {
            try
            {
                var filter = status == "all"
                    ? FilterDefinition<Package>.Empty
                    : Builders<Package>.Filter.Regex(
                        p => p.ApprovalStatus,
                        new MongoDB.Bson.BsonRegularExpression($"^{status}$", "i"));

                var packages = await _context.Packages.Find(filter).ToListAsync();

                if (!string.IsNullOrEmpty(search))
                {
                    packages = packages.Where(p =>
                        p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (p.Authors?.Any(a => a.Contains(search, StringComparison.OrdinalIgnoreCase)) ?? false))
                        .ToList();
                }

                return sortBy switch
                {
                    "oldest" => packages.OrderBy(p => p.CreatedAt).ToList(),
                    "name" => packages.OrderBy(p => p.Name).ToList(),
                    "downloads" => packages.OrderByDescending(p => p.Downloads).ToList(),
                    _ => packages.OrderByDescending(p => p.CreatedAt).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving packages by status {Status}", status);
                return new List<Package>();
            }
        }

        public async Task<int> GetPackageCountByStatusAsync(string status)
        {
            try
            {
                if (status == "all")
                    return (int)await _context.Packages.CountDocumentsAsync(FilterDefinition<Package>.Empty);

                var filter = Builders<Package>.Filter.Regex(
                    p => p.ApprovalStatus,
                    new MongoDB.Bson.BsonRegularExpression($"^{status}$", "i"));

                return (int)await _context.Packages.CountDocumentsAsync(filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving package count by status {Status}", status);
                return 0;
            }
        }

        public async Task<bool> ApprovePackageAsync(string packageId, string adminUserId)
        {
            try
            {
                var package = await _context.Packages.Find(p => p.Id == packageId).FirstOrDefaultAsync();
                if (package == null) return false;

                var update = Builders<Package>.Update
                    .Set(p => p.ApprovalStatus, "Approved")
                    .Set(p => p.IsActive, true)
                    .Set(p => p.UpdatedBy, adminUserId)
                    .Set(p => p.LastUpdated, DateTime.UtcNow);

                await _context.Packages.UpdateOneAsync(p => p.Id == packageId, update);
                await LogActivityAsync("approve", $"Approved package '{package.Name}'", adminUserId);
                _logger.LogInformation("Package {PackageId} approved by {AdminId}", packageId, adminUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving package {PackageId}", packageId);
                return false;
            }
        }

        public async Task<bool> RejectPackageAsync(string packageId, string adminUserId, string? reason = null)
        {
            try
            {
                var package = await _context.Packages.Find(p => p.Id == packageId).FirstOrDefaultAsync();
                if (package == null) return false;

                var update = Builders<Package>.Update
                    .Set(p => p.ApprovalStatus, "Rejected")
                    .Set(p => p.IsActive, false)
                    .Set(p => p.UpdatedBy, adminUserId)
                    .Set(p => p.LastUpdated, DateTime.UtcNow)
                    .Set(p => p.RejectionReason, reason);

                await _context.Packages.UpdateOneAsync(p => p.Id == packageId, update);

                var desc = $"Rejected package '{package.Name}'" + (string.IsNullOrEmpty(reason) ? "" : $": {reason}");
                await LogActivityAsync("reject", desc, adminUserId);
                _logger.LogInformation("Package {PackageId} rejected by {AdminId}", packageId, adminUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting package {PackageId}", packageId);
                return false;
            }
        }

        public async Task<bool> TogglePackageStatusAsync(string packageId, string adminUserId)
        {
            try
            {
                var package = await _context.Packages.Find(p => p.Id == packageId).FirstOrDefaultAsync();
                if (package == null) return false;

                var newActive = !package.IsActive;
                var update = Builders<Package>.Update
                    .Set(p => p.IsActive, newActive)
                    .Set(p => p.UpdatedBy, adminUserId)
                    .Set(p => p.LastUpdated, DateTime.UtcNow);

                await _context.Packages.UpdateOneAsync(p => p.Id == packageId, update);

                var action = newActive ? "enable" : "disable";
                await LogActivityAsync(action, $"{char.ToUpper(action[0]) + action[1..]}d package '{package.Name}'", adminUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling package status {PackageId}", packageId);
                return false;
            }
        }

        public async Task<List<AdminActivity>> GetRecentActivityAsync()
        {
            try
            {
                var entities = await _context.AdminActivities
                    .Find(FilterDefinition<AdminActivityEntity>.Empty)
                    .SortByDescending(a => a.Timestamp)
                    .Limit(10)
                    .ToListAsync();

                return entities.Select(a => new AdminActivity
                {
                    Id = a.Id,
                    Action = a.Action,
                    Description = a.Description,
                    UserId = a.UserId,
                    Username = a.Username,
                    Timestamp = a.Timestamp,
                    Icon = GetActivityIcon(a.Action),
                    Color = GetActivityColor(a.Action)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent admin activity");
                return new List<AdminActivity>();
            }
        }

        public async Task LogActivityAsync(string action, string description, string userId)
        {
            try
            {
                var user = await _context.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
                var activity = new AdminActivityEntity
                {
                    Action = action,
                    Description = description,
                    UserId = userId,
                    Username = user?.Username ?? "Admin",
                    Timestamp = DateTime.UtcNow
                };

                await _context.AdminActivities.InsertOneAsync(activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging admin activity");
            }
        }

        private static string GetActivityIcon(string action) => action.ToLower() switch
        {
            "approve" => "check-circle",
            "reject" => "x-circle",
            "enable" => "play-circle",
            "disable" => "pause-circle",
            "delete" => "trash",
            _ => "info-circle"
        };

        private static string GetActivityColor(string action) => action.ToLower() switch
        {
            "approve" => "success",
            "reject" => "danger",
            "enable" => "success",
            "disable" => "warning",
            "delete" => "danger",
            _ => "primary"
        };
    }
}
