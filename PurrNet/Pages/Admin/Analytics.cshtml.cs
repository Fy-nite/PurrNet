using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purrnet.Models;
using Purrnet.Services;

namespace Purrnet.Pages.Admin
{
    [Authorize]
    public class AnalyticsModel : BasePageModel
    {
        private readonly IPackageService _packageService;

        public PackageStatistics? Statistics { get; set; }
        public List<Package> AllPackages { get; set; } = new();

        public AnalyticsModel(IPackageService packageService, TestingModeService testingModeService)
            : base(testingModeService)
        {
            _packageService = packageService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!User.HasClaim("IsAdmin", "True"))
                return Forbid();

            Statistics = await _packageService.GetStatisticsAsync();
            AllPackages = (await _packageService.GetAllPackagesAsync())
                .OrderByDescending(p => p.Downloads)
                .ToList();

            return Page();
        }
    }
}
