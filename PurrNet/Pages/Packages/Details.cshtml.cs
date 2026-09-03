using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purrnet.Models;
using Purrnet.Services;
using System.ComponentModel.DataAnnotations;

namespace Purrnet.Pages.Packages
{
    public class DetailsModel : PageModel
    {
        private readonly IPackageService _packageService;
        private readonly ILogger<DetailsModel> _logger;

        public Package? Package { get; set; }
        public string? ErrorMessage { get; set; }
        public List<PackageReview> Reviews { get; set; } = new();

        [BindProperty]
        [Range(1,5, ErrorMessage="Rating must be 1-5")]
        public int ReviewRating { get; set; } = 5;

        [BindProperty]
        public string? ReviewTitle { get; set; }

        [BindProperty]
        [Required(ErrorMessage="Review body required")]
        public string ReviewBody { get; set; } = string.Empty;

        public string? ReviewMessage { get; set; }
        public bool ReviewSuccess { get; set; }

        public DetailsModel(IPackageService packageService, ILogger<DetailsModel> logger)
        {
            _packageService = packageService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(string packageName, string? version = null)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                ErrorMessage = "Package name is required.";
                return Page();
            }

            try
            {
                Package = await _packageService.GetPackageAsync(packageName, version);
                
                if (Package == null)
                {
                    ErrorMessage = $"Package '{packageName}' not found.";
                    return Page();
                }

                // Increment view count (fire-and-forget — don't block page load, but must not share DbContext)
                _ = Task.Run(async () =>
                {
                    using var scope = HttpContext.RequestServices.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IPackageService>();
                    await svc.IncrementViewCountAsync(Package.Id.ToString());
                });
                Reviews = await _packageService.GetPackageReviewsAsync(packageName);
                
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading package details for {PackageName}", packageName);
                ErrorMessage = "An error occurred while loading the package details.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostReviewAsync(string packageName)
        {
            // Load package for redisplay
            Package = await _packageService.GetPackageAsync(packageName);
            if (Package == null)
            {
                ErrorMessage = $"Package '{packageName}' not found.";
                return Page();
            }
            Reviews = await _packageService.GetPackageReviewsAsync(packageName);

            if (!ModelState.IsValid)
            {
                ReviewMessage = "Please fix review errors.";
                ReviewSuccess = false;
                return Page();
            }

            if (string.IsNullOrWhiteSpace(ReviewBody))
            {
                ModelState.AddModelError(nameof(ReviewBody), "Body required");
                ReviewMessage = "Review body required.";
                ReviewSuccess = false;
                return Page();
            }

            var userId = User.FindFirst("UserId")?.Value;
            var reviewerName = User.Identity?.Name ?? (User.Identity?.IsAuthenticated == true ? User.Identity.Name! : "Anonymous");
            if (string.IsNullOrWhiteSpace(reviewerName)) reviewerName = "Anonymous";
            var avatar = User.FindFirst("urn:github:avatar")?.Value;

            var (success, error) = await _packageService.AddPackageReviewAsync(packageName, userId, reviewerName, avatar, ReviewRating, ReviewTitle ?? string.Empty, ReviewBody);
            if (success)
            {
                ReviewMessage = "Review submitted!";
                ReviewSuccess = true;
                ModelState.Clear();
                ReviewBody = string.Empty;
                ReviewTitle = string.Empty;
                ReviewRating = 5;
                Reviews = await _packageService.GetPackageReviewsAsync(packageName);
                Package = await _packageService.GetPackageAsync(packageName); // refresh rating
            }
            else
            {
                ReviewMessage = error ?? "Failed to submit review.";
                ReviewSuccess = false;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteReviewAsync(string packageName, string reviewId)
        {
            // Delete handler shares the same PageModel with ReviewRating/ReviewBody [Required] — clear it so delete doesn't fail ModelState
            ModelState.Clear();
            var userId = User.FindFirst("UserId")?.Value;
            bool isAdmin = User.HasClaim("IsAdmin", "1") || User.HasClaim("IsAdmin", "True");
            var deleted = await _packageService.DeleteReviewAsync(reviewId, userId, isAdmin);
            // reload (bypass cache that may still hold deleted review)
            Package = await _packageService.GetPackageAsync(packageName);
            Reviews = await _packageService.GetPackageReviewsAsync(packageName);
            // force fresh read after delete
            Reviews = await _packageService.GetPackageReviewsAsync(packageName);
            ReviewMessage = deleted ? "Review deleted." : "Not authorized.";
            ReviewSuccess = deleted;
            return Page();
        }
    }
}
