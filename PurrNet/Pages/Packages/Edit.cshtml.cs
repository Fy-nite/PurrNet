using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Purrnet.Models;
using Purrnet.Services;

namespace Purrnet.Pages.Packages
{
    [Authorize]
    public class EditModel : BasePageModel
    {
        private readonly IPackageService _packageService;
        private readonly ILogger<EditModel> _logger;

        [BindProperty]
        public int PackageId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Package name is required")]
        [Display(Name = "Package Name")]
        public string PackageName { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Version is required")]
        public string Version { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "At least one author is required")]
        [Display(Name = "Authors (comma-separated)")]
        public string Authors { get; set; } = string.Empty;

        [BindProperty]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [BindProperty]
        [Display(Name = "README URL")]
        public string? ReadmeUrl { get; set; }

        [BindProperty]
        [Display(Name = "Homepage URL")]
        public string? Homepage { get; set; }

        [BindProperty]
        [Display(Name = "Issue Tracker URL")]
        public string? IssueTracker { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Git repository URL is required")]
        [Display(Name = "Git Repository URL")]
        public string Git { get; set; } = string.Empty;

        [BindProperty]
        [Display(Name = "License")]
        public string? License { get; set; }

        [BindProperty]
        [Display(Name = "License URL")]
        public string? LicenseUrl { get; set; }

        [BindProperty]
        [Display(Name = "Installer Script")]
        public string? Installer { get; set; }

        [BindProperty]
        [Display(Name = "Dependencies (comma-separated, format: name@version)")]
        public string? Dependencies { get; set; }

        [BindProperty]
        [Display(Name = "Keywords (comma-separated)")]
        public string? Keywords { get; set; }

        [BindProperty]
        [Display(Name = "Supported Platforms (comma-separated)")]
        public string? SupportedPlatforms { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Main file is required")]
        [Display(Name = "Main File")]
        public string MainFile { get; set; } = string.Empty;

        [BindProperty]
        public bool IsLibrary { get; set; }

        public string? Message { get; set; }
        public bool IsSuccess { get; set; }
        public Package? OriginalPackage { get; set; }

        public EditModel(IPackageService packageService, ILogger<EditModel> logger, TestingModeService testingModeService)
            : base(testingModeService)
        {
            _packageService = packageService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                return NotFound();
            }

            var package = await _packageService.GetPackageAsync(packageName);
            if (package == null)
            {
                return NotFound();
            }

            // Check if user is authorized to edit this package
            var userIdClaim = User.FindFirst("UserId");
            var isAdmin = User.HasClaim("IsAdmin", "True");
            
            if (!isAdmin && (userIdClaim == null || package.OwnerId == null || 
                !int.TryParse(userIdClaim.Value, out var userId) || userId != package.OwnerId))
            {
                return Forbid();
            }

            OriginalPackage = package;
            
            // Populate form fields with existing data
            PackageId = package.Id;
            PackageName = package.Name;
            Version = package.Version;
            Authors = string.Join(", ", package.AuthorsList);
            Description = package.Description;
            ReadmeUrl = package.ReadmeUrl;
            Homepage = package.Homepage;
            IssueTracker = package.IssueTracker;
            Git = package.Git;
            License = package.License;
            LicenseUrl = package.LicenseUrl;
            Installer = package.Installer;
            MainFile = package.MainFile ?? string.Empty;
            IsLibrary = package.IsLibrary == 1;
            Dependencies = string.Join(", ", package.DependenciesList);
            Keywords = string.Join(", ", package.KeywordsList);
            SupportedPlatforms = string.Join(", ", package.SupportedPlatformsList);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Reload original package data for display
                OriginalPackage = await _packageService.GetPackageByIdAsync(PackageId.ToString());
                return Page();
            }

            try
            {
                // Verify the package exists and user has permission
                var existingPackage = await _packageService.GetPackageByIdAsync(PackageId.ToString());
                if (existingPackage == null)
                {
                    return NotFound();
                }

                var userIdClaim = User.FindFirst("UserId");
                var isAdmin = User.HasClaim("IsAdmin", "True");
                
                if (!isAdmin && (userIdClaim == null || existingPackage.OwnerId == null || 
                    !int.TryParse(userIdClaim.Value, out var userId) || userId != existingPackage.OwnerId))
                {
                    return Forbid();
                }

                // Parse form data
                var dependenciesList = new List<string>();
                if (!string.IsNullOrWhiteSpace(Dependencies))
                {
                    dependenciesList = Dependencies.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(d => d.Trim())
                        .Where(d => !string.IsNullOrEmpty(d))
                        .ToList();
                }

                var keywordsList = new List<string>();
                if (!string.IsNullOrWhiteSpace(Keywords))
                {
                    keywordsList = Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();
                }

                var authorsList = Authors.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrEmpty(a))
                    .ToList();

                var platformsList = new List<string>();
                if (!string.IsNullOrWhiteSpace(SupportedPlatforms))
                {
                    platformsList = SupportedPlatforms.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();
                }

                if (authorsList.Count == 0)
                {
                    ModelState.AddModelError(nameof(Authors), "At least one author is required");
                    OriginalPackage = existingPackage;
                    return Page();
                }

                var PurrConfig = new PurrConfig
                {
                    Name = PackageName.Trim(),
                    Version = Version.Trim(),
                    Authors = authorsList,
                    SupportedPlatforms = platformsList,
                    Description = Description?.Trim() ?? string.Empty,
                    ReadmeUrl = ReadmeUrl?.Trim() ?? string.Empty,
                    License = License?.Trim() ?? string.Empty,
                    LicenseUrl = LicenseUrl?.Trim() ?? string.Empty,
                    Homepage = Homepage?.Trim() ?? string.Empty,
                    IssueTracker = IssueTracker?.Trim() ?? string.Empty,
                    Git = Git.Trim(),
                    Installer = Installer?.Trim() ?? string.Empty,
                    MainFile = MainFile?.Trim() ?? string.Empty,
                    IsLibrary = IsLibrary,
                    Dependencies = dependenciesList,
                    Keywords = keywordsList
                };

                var userName = User.Identity?.Name ?? "unknown";
                var success = await _packageService.UpdatePackageAsync(PackageId.ToString(), PurrConfig, userName);

                if (success)
                {
                    Message = "Package updated successfully!";
                    IsSuccess = true;
                    
                    // Redirect to package details page
                    return RedirectToPage("/Packages/Details", new { packageName = PurrConfig.Name });
                }
                else
                {
                    Message = "Failed to update package.";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating package {PackageId}", PackageId);
                Message = "An error occurred while updating the package.";
                IsSuccess = false;
            }

            OriginalPackage = await _packageService.GetPackageByIdAsync(PackageId.ToString());
            return Page();
        }
    }
}
