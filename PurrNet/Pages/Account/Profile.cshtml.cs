using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Purrnet.Models;
using Purrnet.Services;

namespace Purrnet.Pages.Account
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<ProfileModel> _logger;

        public User? CurrentUser { get; set; }
        public List<Package> OwnedPackages { get; set; } = new();
        public List<Package> MaintainedPackages { get; set; } = new();
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }

        public ProfileModel(IUserService userService, ILogger<ProfileModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Prefer the GitHub NameIdentifier claim which is set during OAuth
                var gitHubId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(gitHubId))
                {
                    // No GitHub identifier found — require authentication
                    return RedirectToPage("/Account/Login");
                }

                CurrentUser = await _userService.GetUserByGitHubIdAsync(gitHubId);

                if (CurrentUser != null)
                {
                    OwnedPackages = await _userService.GetUserPackagesAsync(CurrentUser.Id);
                    MaintainedPackages = await _userService.GetUserMaintainedPackagesAsync(CurrentUser.Id);
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user profile for GitHub ID {GitHubId}",
                    User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
                Message = "An error occurred while loading your profile.";
                IsSuccess = false;
                return Page();
            }
        }
    }
}
