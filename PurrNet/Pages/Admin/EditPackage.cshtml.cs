using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purrnet.Models;
using Purrnet.Services;

namespace Purrnet.Pages.Admin
{
    [Authorize]
    public class EditPackageModel : BasePageModel
    {
        private readonly IPackageService _packageService;

        public Package? Package { get; set; }
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }

        public EditPackageModel(IPackageService packageService, TestingModeService testingModeService)
            : base(testingModeService)
        {
            _packageService = packageService;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!User.HasClaim("IsAdmin", "True"))
                return Forbid();

            Package = await _packageService.GetPackageByIdAsync(id);
            if (Package == null)
                return NotFound();

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
            string? MainFile,
            string? Dependencies,
            string? Categories,
            string? License,
            string? SupportedPlatforms,
            string? IconUrl)
        {
            if (!User.HasClaim("IsAdmin", "True"))
                return Forbid();

            Package = await _packageService.GetPackageByIdAsync(id);
            if (Package == null)
                return NotFound();

            static List<string> Split(string? s) =>
                string.IsNullOrWhiteSpace(s)
                    ? new List<string>()
                    : s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var config = new PurrConfig
            {
                Name = Package.Name,
                Version = Version?.Trim() ?? Package.Version,
                Description = Description ?? string.Empty,
                Authors = Split(Authors),
                Git = Git?.Trim() ?? Package.Git,
                Homepage = Homepage ?? string.Empty,
                IssueTracker = IssueTracker ?? string.Empty,
                Installer = Installer ?? string.Empty,
                Dependencies = Split(Dependencies),
                Categories = Split(Categories),
                License = License ?? string.Empty,
                SupportedPlatforms = Split(SupportedPlatforms),
                IconUrl = IconUrl?.Trim() ?? string.Empty,
                MainFile = MainFile?.Trim()
            };

            var updatedBy = User.Identity?.Name ?? "admin";
            var success = await _packageService.UpdatePackageAsync(id, config, updatedBy);

            if (success)
            {
                Message = "Package updated successfully.";
                IsSuccess = true;
                // Reload with fresh data
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
