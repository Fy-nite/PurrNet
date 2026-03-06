using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purrnet.Models;
using Purrnet.Services;

namespace Purrnet.Pages.Users
{
    public class ProfileModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<ProfileModel> _logger;

        public User? ProfileUser { get; set; }
        public List<Package> OwnedPackages { get; set; } = new();
        public List<Package> MaintainedPackages { get; set; } = new();
        public bool IsOwnProfile { get; set; }
        public string? ErrorMessage { get; set; }

        public ProfileModel(IUserService userService, ILogger<ProfileModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return NotFound();
            }

            try
            {
                ProfileUser = await _userService.GetUserByUsernameAsync(username);
                
                if (ProfileUser == null)
                {
                    ErrorMessage = $"User '{username}' not found.";
                    return NotFound();
                }

                OwnedPackages = await _userService.GetUserPackagesAsync(ProfileUser.Id);
                MaintainedPackages = await _userService.GetUserMaintainedPackagesAsync(ProfileUser.Id);
                
                // Check if this is the current user's profile
                if (User.Identity?.IsAuthenticated == true)
                {
                    var currentUserIdClaim = User.FindFirst("UserId");
                    if (currentUserIdClaim != null)
                    {
                        IsOwnProfile = currentUserIdClaim.Value == ProfileUser.Id;
                    }
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile for user {Username}", username);
                ErrorMessage = "An error occurred while loading the user profile.";
                return NotFound();
            }
        }
    }
}
