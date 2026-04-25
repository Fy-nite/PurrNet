using Purrnet.Models;

namespace Purrnet.Services
{
    public interface IAdminService
    {
        Task<List<Package>> GetPendingPackagesAsync();
        Task<List<Package>> GetPackagesByStatusAsync(string status, string? search = null, string? sortBy = null);
        Task<int> GetPackageCountByStatusAsync(string status);
        Task<bool> ApprovePackageAsync(string packageId, string adminUserId);
        Task<bool> RejectPackageAsync(string packageId, string adminUserId, string? reason = null);
        Task<bool> TogglePackageStatusAsync(string packageId, string adminUserId);
        Task<List<AdminActivity>> GetRecentActivityAsync();
        Task LogActivityAsync(string action, string description, string userId);
    }

    public class AdminActivity
    {
        public string Id { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Icon { get; set; } = "info-circle";
        public string Color { get; set; } = "primary";
    }
}
