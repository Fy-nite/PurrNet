using Microsoft.EntityFrameworkCore;
using Purrnet.Data;
using Purrnet.Models;

namespace Purrnet.Services
{
    public class AdminService : IAdminService
    {
        private readonly PurrNetDbContext _context;
        private readonly ILogger<AdminService> _logger;

        public AdminService(PurrNetDbContext context, ILogger<AdminService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Package>> GetPendingPackagesAsync()
        {
            return await _context.Packages.Where(p => p.ApprovalStatus == "Pending").OrderBy(p => p.CreatedAt).ToListAsync();
        }

        public async Task<List<Package>> GetPackagesByStatusAsync(string status, string? search = null, string? sortBy = null)
        {
            var query = _context.Packages.AsQueryable();
            if (status != "all") query = query.Where(p => p.ApprovalStatus == status);
            var packages = await query.ToListAsync();
            return packages;
        }

        public async Task<int> GetPackageCountByStatusAsync(string status)
        {
            return status == "all" ? await _context.Packages.CountAsync() : await _context.Packages.CountAsync(p => p.ApprovalStatus == status);
        }

        public async Task<bool> ApprovePackageAsync(string packageId, string adminUserId)
        {
            var package = await _context.Packages.FindAsync(int.Parse(packageId));
            if (package == null) return false;
            package.ApprovalStatus = "Approved";
            package.IsActive = 1;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectPackageAsync(string packageId, string adminUserId, string? reason = null)
        {
            var package = await _context.Packages.FindAsync(int.Parse(packageId));
            if (package == null) return false;
            package.ApprovalStatus = "Rejected";
            package.IsActive = 0;
            package.RejectionReason = reason;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> TogglePackageStatusAsync(string packageId, string adminUserId)
        {
            var package = await _context.Packages.FindAsync(int.Parse(packageId));
            if (package == null) return false;
            package.IsActive = package.IsActive == 1 ? 0 : 1;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<AdminActivity>> GetRecentActivityAsync()
        {
            var entities = await _context.AdminActivities.OrderByDescending(a => a.Timestamp).Take(10).ToListAsync();
            return entities.Select(a => new AdminActivity
            {
                Id = 0,
                Action = a.Action,
                Description = a.Description,
                UserId = a.UserId,
                Username = a.Username,
                Timestamp = DateTime.Parse(a.Timestamp)
            }).ToList();
        }

        public async Task LogActivityAsync(string action, string description, string userId)
        {
            var activity = new AdminActivityEntity
            {
                Action = action,
                Description = description,
                UserId = userId,
                Timestamp = DateTime.UtcNow.ToString("O")
            };
            _context.AdminActivities.Add(activity);
            await _context.SaveChangesAsync();
        }

        private static string GetActivityIcon(string action) => "info-circle";
        private static string GetActivityColor(string action) => "primary";
    }
}
