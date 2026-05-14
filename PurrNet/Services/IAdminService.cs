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
}
