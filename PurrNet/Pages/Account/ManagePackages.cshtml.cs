using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purrnet.Models;
using Purrnet.Services;

namespace Purrnet.Pages.Account
{
    [Authorize]
    public class ManagePackagesModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly IPackageService _packageService;
        private readonly ILogger<ManagePackagesModel> _logger;

        public List<Package> OwnedPackages { get; set; } = new();
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }

        public ManagePackagesModel(IUserService userService, IPackageService packageService, ILogger<ManagePackagesModel> logger)
        {
            _userService = userService;
            _packageService = packageService;
            _logger = logger;
        }

        private async Task<int?> GetCurrentUserIdAsync()
        {
            var gitHubId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(gitHubId)) return null;
            var user = await _userService.GetUserByGitHubIdAsync(gitHubId);
            return user?.Id;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return RedirectToPage("/Account/Login");

            OwnedPackages = await _userService.GetUserPackagesAsync(userId.Value);
            return Page();
        }

        public async Task<IActionResult> OnPostToggleAsync(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return RedirectToPage("/Account/Login");

            var package = await _packageService.GetPackageByIdAsync(id);
            if (package == null || package.OwnerId != userId)
            {
                Message = "Package not found or you don't have permission to modify it.";
                IsSuccess = false;
                OwnedPackages = await _userService.GetUserPackagesAsync(userId.Value);
                return Page();
            }

            var success = await _packageService.TogglePackageStatusAsync(id);
            Message = success ? "Package status updated." : "Failed to update package status.";
            IsSuccess = success;

            OwnedPackages = await _userService.GetUserPackagesAsync(userId.Value);
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return RedirectToPage("/Account/Login");

            var package = await _packageService.GetPackageByIdAsync(id);
            if (package == null || package.OwnerId != userId)
            {
                Message = "Package not found or you don't have permission to delete it.";
                IsSuccess = false;
                OwnedPackages = await _userService.GetUserPackagesAsync(userId.Value);
                return Page();
            }

            var success = await _packageService.DeletePackageAsync(id);
            Message = success ? "Package deleted." : "Failed to delete package.";
            IsSuccess = success;

            OwnedPackages = await _userService.GetUserPackagesAsync(userId.Value);
            return Page();
        }
    }
}
