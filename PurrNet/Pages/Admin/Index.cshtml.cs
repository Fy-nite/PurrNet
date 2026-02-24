using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purrnet.Models;
using Purrnet.Services;

namespace Purrnet.Pages.Admin
{
    [Authorize]
    public class IndexModel : BasePageModel
    {
        private readonly IPackageService _packageService;
        private readonly IUserService _userService;

        public List<Package> Packages { get; set; } = new();
        public List<Package> PendingPackages { get; set; } = new();
        public PackageStatistics? Statistics { get; set; }
        public List<Purrnet.Models.AdminActivity> RecentActivity { get; set; } = new();
        public List<User> AllUsers { get; set; } = new();
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }

        public IndexModel(IPackageService packageService, IUserService userService, TestingModeService testingModeService) 
            : base(testingModeService)
        {
            _packageService = packageService;
            _userService = userService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if user is admin
            if (!User.HasClaim("IsAdmin", "True"))
            {
                return Forbid();
            }

            Packages = await _packageService.GetAllPackagesAsync();
            Statistics = await _packageService.GetStatisticsAsync();
            AllUsers = await _userService.GetAllUsersAsync();
            
            return Page();
        }

        public async Task<IActionResult> OnPostPromoteUserAsync(int userId)
        {
            if (!User.HasClaim("IsAdmin", "True"))
            {
                return Forbid();
            }

            var success = await _userService.PromoteToAdminAsync(userId);
            
            if (success)
            {
                Message = "User successfully promoted to admin.";
                IsSuccess = true;
            }
            else
            {
                Message = "Failed to promote user to admin.";
                IsSuccess = false;
            }

            // Reload data
            Packages = await _packageService.GetAllPackagesAsync();
            Statistics = await _packageService.GetStatisticsAsync();
            AllUsers = await _userService.GetAllUsersAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostRevokeAdminAsync(int userId)
        {
            if (!User.HasClaim("IsAdmin", "True"))
            {
                return Forbid();
            }

            // Prevent revoking your own admin privileges
            var currentUserIdClaim = User.FindFirst("UserId");
            if (currentUserIdClaim != null && int.TryParse(currentUserIdClaim.Value, out var currentUserId))
            {
                if (currentUserId == userId)
                {
                    Message = "You cannot revoke your own admin privileges.";
                    IsSuccess = false;

                    // Reload data
                    Packages = await _packageService.GetAllPackagesAsync();
                    Statistics = await _packageService.GetStatisticsAsync();
                    AllUsers = await _userService.GetAllUsersAsync();

                    return Page();
                }
            }

            var success = await _userService.RevokeAdminAsync(userId);

            if (success)
            {
                Message = "Admin privileges successfully revoked.";
                IsSuccess = true;
            }
            else
            {
                Message = "Failed to revoke admin privileges.";
                IsSuccess = false;
            }

            // Reload data
            Packages = await _packageService.GetAllPackagesAsync();
            Statistics = await _packageService.GetStatisticsAsync();
            AllUsers = await _userService.GetAllUsersAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostBanUserAsync(int userId)
        {
            if (!User.HasClaim("IsAdmin", "True")) return Forbid();

            var success = await _userService.BanUserAsync(userId);
            Message = success ? "User banned." : "Failed to ban user.";
            IsSuccess = success;

            Packages = await _packageService.GetAllPackagesAsync();
            Statistics = await _packageService.GetStatisticsAsync();
            AllUsers = await _userService.GetAllUsersAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostUnbanUserAsync(int userId)
        {
            if (!User.HasClaim("IsAdmin", "True")) return Forbid();

            var success = await _userService.UnbanUserAsync(userId);
            Message = success ? "User unbanned." : "Failed to unban user.";
            IsSuccess = success;

            Packages = await _packageService.GetAllPackagesAsync();
            Statistics = await _packageService.GetStatisticsAsync();
            AllUsers = await _userService.GetAllUsersAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostReportOutdatedAsync(int packageId)
        {
            if (!User.HasClaim("IsAdmin", "True")) return Forbid();

            var success = await _packageService.MarkPackageOutdatedAsync(packageId, true);
            Message = success ? "Package flagged as outdated." : "Failed to flag package.";
            IsSuccess = success;

            Packages = await _packageService.GetAllPackagesAsync();
            Statistics = await _packageService.GetStatisticsAsync();
            AllUsers = await _userService.GetAllUsersAsync();

            return Page();
        }
    }
}
