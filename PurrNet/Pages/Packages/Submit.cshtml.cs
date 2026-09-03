using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Purrnet.Models;
using Purrnet.Services;

namespace Purrnet.Pages.Packages
{
    [Authorize]
    public class SubmitModel : BasePageModel
    {
        private readonly IPackageService _packageService;
        private readonly ILogger<SubmitModel> _logger;

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
        [Display(Name = "Installer Script")]
        public string? Installer { get; set; }

        [BindProperty]
        [Display(Name = "Dependencies (comma-separated, format: name@version)")]
        public string? Dependencies { get; set; }

        [BindProperty]
        [Display(Name = "Keywords (comma-separated)")]
        public string? Keywords { get; set; }

        [BindProperty]
        [Display(Name = "Categories (comma-separated)")]
        public string? Categories { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Main file is required (binary to symlink)")]
        [Display(Name = "Main File (executable to symlink)")]
        public string MainFile { get; set; } = string.Empty;

        [BindProperty]
        [Display(Name = "Icon URL")]
        public string? IconUrl { get; set; }

        [BindProperty]
        [Display(Name = "License")]
        public string? License { get; set; }

        [BindProperty]
        [Display(Name = "Supported Platforms (comma-separated)")]
        public string? SupportedPlatforms { get; set; }

        [BindProperty]
        public bool IsLibrary { get; set; }

        public string? Message { get; set; }
        public bool IsSuccess { get; set; }

        public SubmitModel(IPackageService packageService, ILogger<SubmitModel> logger, TestingModeService testingModeService)
            : base(testingModeService)
        {
            _packageService = packageService;
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Parse dependencies safely
                var dependenciesList = new List<string>();
                if (!string.IsNullOrWhiteSpace(Dependencies))
                {
                    dependenciesList = Dependencies.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(d => d.Trim())
                        .Where(d => !string.IsNullOrEmpty(d))
                        .ToList();
                }

                // Parse keywords safely
                var keywordsList = new List<string>();
                if (!string.IsNullOrWhiteSpace(Keywords))
                {
                    keywordsList = Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();
                }

                // Parse categories safely
                var categoriesList = new List<string>();
                if (!string.IsNullOrWhiteSpace(Categories))
                {
                    categoriesList = Categories.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim())
                        .Where(c => !string.IsNullOrEmpty(c))
                        .ToList();
                }

                // Parse authors safely
                var authorsList = Authors.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrEmpty(a))
                    .ToList();

                if (authorsList.Count == 0)
                {
                    ModelState.AddModelError(nameof(Authors), "At least one author is required");
                    return Page();
                }

                var PurrConfig = new PurrConfig
                {
                    Name = PackageName.Trim(),
                    Version = Version.Trim(),
                    Authors = authorsList,
                    Description = Description?.Trim() ?? string.Empty,
                    ReadmeUrl = ReadmeUrl?.Trim() ?? string.Empty,
                    Homepage = Homepage?.Trim() ?? string.Empty,
                    IssueTracker = IssueTracker?.Trim() ?? string.Empty,
                    Git = Git.Trim(),
                    Installer = Installer?.Trim() ?? string.Empty,
                    Dependencies = dependenciesList,
                    Keywords = keywordsList,
                    Categories = categoriesList,
                    MainFile = MainFile.Trim(),
                    IconUrl = IconUrl?.Trim() ?? string.Empty,
                    License = License?.Trim() ?? string.Empty,
                    SupportedPlatforms = string.IsNullOrWhiteSpace(SupportedPlatforms) ? new List<string>() : SupportedPlatforms.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                    IsLibrary = IsLibrary
                };

                // Get current user info for package creation tracking
                var userName = User.Identity?.Name ?? "unknown";
                var userIdClaim = User.FindFirst("UserId");
                string? userIdStr = userIdClaim?.Value;
                
                var success = await _packageService.SavePackageAsync(PurrConfig, userName, userIdStr);

                if (success)
                {
                    Message = "Package submitted successfully!";
                    IsSuccess = true;
                    ModelState.Clear();
                    
                    // Clear form fields
                    PackageName = string.Empty;
                    Version = string.Empty;
                    Authors = string.Empty;
                    Description = string.Empty;
                    ReadmeUrl = null;
                    Homepage = null;
                    IssueTracker = null;
                    Git = string.Empty;
                    Installer = null;
                    Dependencies = null;
                    Keywords = null;
                    Categories = null;
                    MainFile = string.Empty;
                    IconUrl = null;
                    License = null;
                    SupportedPlatforms = null;
                    
                    // Redirect to package details page
                    return RedirectToPage("/Packages/Details", new { packageName = PurrConfig.Name });
                }
                else
                {
                    Message = "Failed to submit package. Package may already exist.";
                    IsSuccess = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting package");
                Message = "An error occurred while submitting the package.";
                IsSuccess = false;
            }

            return Page();
        }
    }
}
