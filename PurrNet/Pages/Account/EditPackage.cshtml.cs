using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purrnet.Models;
using Purrnet.Services;

namespace Purrnet.Pages.Account
{
    [Authorize]
    public class EditPackageModel : PageModel
    {
        private readonly IPackageService _packageService;
        private readonly IUserService _userService;

        public Package? Package { get; set; }
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }

        public EditPackageModel(IPackageService packageService, IUserService userService)
        {
            _packageService = packageService;
            _userService = userService;
        }

        private async Task<int?> GetCurrentUserIdAsync()
        {
            var gitHubId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(gitHubId)) return null;
            var user = await _userService.GetUserByGitHubIdAsync(gitHubId);
            return user?.Id;
        }

        private bool IsAdmin => User.HasClaim("IsAdmin", "True");

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return RedirectToPage("/Account/Login");

            Package = await _packageService.GetPackageByIdAsync(id);
            if (Package == null) return NotFound();

            if (!IsAdmin && Package.OwnerId != userId)
                return Forbid();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(
            int id,
            string Version,
            string? Description,
            string? Authors,
            string? Git,
            string? Homepage,
            string? IssueTracker,
            string? Installer,
            string? Dependencies,
            string? Categories,
            string? Keywords,
            string? License,
            string? SupportedPlatforms,
            string? IconUrl)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return RedirectToPage("/Account/Login");

            Package = await _packageService.GetPackageByIdAsync(id);
            if (Package == null) return NotFound();

            if (!IsAdmin && Package.OwnerId != userId)
                return Forbid();

            static List<string> Split(string? s) =>
                string.IsNullOrWhiteSpace(s)
                    ? new List<string>()
                    : s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var config = new PurrConfig
            {
                Name        = Package.Name,
                Version     = Version?.Trim() ?? Package.Version,
                Description = Description ?? string.Empty,
                Authors     = Split(Authors),
                Git         = Git?.Trim() ?? Package.Git,
                Homepage    = Homepage ?? string.Empty,
                IssueTracker    = IssueTracker ?? string.Empty,
                Installer       = Installer ?? string.Empty,
                Dependencies    = Split(Dependencies),
                Categories      = Split(Categories),
                Keywords        = Split(Keywords),
                License         = License ?? string.Empty,
                SupportedPlatforms = Split(SupportedPlatforms),
                IconUrl         = IconUrl?.Trim() ?? string.Empty,
            };

            var updatedBy = User.Identity?.Name ?? "user";
            var success = await _packageService.UpdatePackageAsync(id, config, updatedBy);

            if (success)
            {
                Message = "Package updated successfully.";
                IsSuccess = true;
                Package = await _packageService.GetPackageByIdAsync(id);
            }
            else
            {
                Message = "Failed to update package.";
                IsSuccess = false;
            }

            return Page();
        }
    }
}
