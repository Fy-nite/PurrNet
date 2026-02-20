using Microsoft.EntityFrameworkCore;
using Purrnet.Data;
using Purrnet.Models;

namespace Purrnet.Services
{
    public class AdminService : IAdminService
    {
        private readonly PurrDbContext _context;
        private readonly ILogger<AdminService> _logger;

        public AdminService(PurrDbContext context, ILogger<AdminService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Package>> GetPendingPackagesAsync()
        {
            try
            {
                return await _context.Packages
                    .Where(p => p.ApprovalStatus == "Pending")
                    .OrderBy(p => p.CreatedAt)
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
                var query = _context.Packages.AsQueryable();

                // Apply status filter
                if (status != "all")
                {
                    query = query.Where(p => p.ApprovalStatus.ToLower() == status.ToLower());
                }

                // Apply search filter
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(p => 
                        p.Name.Contains(search) || 
                        p.Description.Contains(search) ||
                        p.Authors.Any(a => a.Contains(search)));
                }

                // Apply sorting
                query = sortBy switch
                {
                    "oldest" => query.OrderBy(p => p.CreatedAt),
                    "name" => query.OrderBy(p => p.Name),
                    "downloads" => query.OrderByDescending(p => p.Downloads),
                    _ => query.OrderByDescending(p => p.CreatedAt) // newest (default)
                };

                return await query.ToListAsync();
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
                    return await _context.Packages.CountAsync();

                return await _context.Packages
                    .CountAsync(p => p.ApprovalStatus.ToLower() == status.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving package count by status {Status}", status);
                return 0;
            }
        }

        public async Task<bool> ApprovePackageAsync(int packageId, string adminUserId)
        {
            try
            {
                var package = await _context.Packages.FindAsync(packageId);
                if (package == null) return false;

                package.ApprovalStatus = "Approved";
                package.IsActive = true;
                package.UpdatedBy = adminUserId;
                package.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                await LogActivityAsync("approve", $"Approved package '{package.Name}'", adminUserId);
                _logger.LogInformation("Package {PackageId} approved by admin {AdminId}", packageId, adminUserId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving package {PackageId}", packageId);
                return false;
            }
        }

        public async Task<bool> RejectPackageAsync(int packageId, string adminUserId, string? reason = null)
        {
            try
            {
                var package = await _context.Packages.FindAsync(packageId);
                if (package == null) return false;

                package.ApprovalStatus = "Rejected";
                package.IsActive = false;
                package.UpdatedBy = adminUserId;
                package.LastUpdated = DateTime.UtcNow;
                package.RejectionReason = reason;

                await _context.SaveChangesAsync();
                
                var description = $"Rejected package '{package.Name}'" + 
                    (string.IsNullOrEmpty(reason) ? "" : $": {reason}");
                await LogActivityAsync("reject", description, adminUserId);
                
                _logger.LogInformation("Package {PackageId} rejected by admin {AdminId}", packageId, adminUserId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting package {PackageId}", packageId);
                return false;
            }
        }

        public async Task<bool> TogglePackageStatusAsync(int packageId, string adminUserId)
        {
            try
            {
                var package = await _context.Packages.FindAsync(packageId);
                if (package == null) return false;

                package.IsActive = !package.IsActive;
                package.UpdatedBy = adminUserId;
                package.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                var action = package.IsActive ? "enable" : "disable";
                var capitalizedAction = char.ToUpper(action[0]) + action.Substring(1);
                await LogActivityAsync(action, $"{capitalizedAction}d package '{package.Name}'", adminUserId);
                
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
                return await _context.AdminActivities
                    .OrderByDescending(a => a.Timestamp)
                    .Take(10)
                    .Select(a => new AdminActivity
                    {
                        Id = a.Id,
                        Action = a.Action,
                        Description = a.Description,
                        //Message = a.Description, // Use Description as Message
                        UserId = a.UserId.ToString(),
                        Username = a.Username,
                        Timestamp = a.Timestamp,
                        Icon = GetActivityIcon(a.Action),
                        Color = GetActivityColor(a.Action)
                    })
                    .ToListAsync();
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
                var activity = new AdminActivityEntity
                {
                    Action = action,
                    Description = description,
                    UserId = int.Parse(userId),
                    Username = "Admin", // You might want to look up the actual username
                    Timestamp = DateTime.UtcNow
                };

                _context.AdminActivities.Add(activity);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging admin activity");
            }
        }

        private static string GetActivityIcon(string action)
        {
            return action.ToLower() switch
            {
                "approve" => "check-circle",
                "reject" => "x-circle",
                "enable" => "play-circle",
                "disable" => "pause-circle",
                "delete" => "trash",
                _ => "info-circle"
            };
        }

        private static string GetActivityColor(string action)
        {
            return action.ToLower() switch
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
}
