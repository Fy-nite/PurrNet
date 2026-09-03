using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Purrnet.Models;
using Purrnet.Services;

namespace Purrnet.Pages.Packages
{
    public class BrowseModel : PageModel
    {
        private readonly IPackageService _packageService;
        private readonly ILogger<BrowseModel> _logger;

        public List<Package> Packages { get; set; } = new();
        public SearchResult? SearchResult { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "name";
        
        [BindProperty(SupportsGet = true)]
        public string SearchQuery { get; set; } = "";

        public BrowseModel(IPackageService packageService, ILogger<BrowseModel> logger)
        {
            _packageService = packageService;
            _logger = logger;
        }

        public async Task OnGetAsync()
        {
            try
            {
                SearchResult = await _packageService.SearchPackagesAsync(SearchQuery, SortBy, 1, 100);
                Packages = SearchResult.Packages;
                
                _logger.LogInformation("Loaded {PackageCount} packages for browse page", Packages.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading packages");
                Packages = new List<Package>();
                SearchResult = new SearchResult();
            }
        }
    }
}
