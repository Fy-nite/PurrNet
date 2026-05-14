using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Purrnet.Models;
using Purrnet.Services;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace Purrnet.Controllers.Api
{
    [ApiController]
    [Route("api/v1/packages")]
    public class PackagesController : ControllerBase
    {
        private readonly ILogger<PackagesController> _logger;
        private readonly IPackageService _packageService;
        private readonly TestingModeService _testingModeService;
        private static readonly string _sanitizeRegex = @"[^\x20-\x7e]+";

        public PackagesController(ILogger<PackagesController> logger, IPackageService packageService, TestingModeService testingModeService)
        {
            _logger = logger;
            _packageService = packageService;
            _testingModeService = testingModeService;
        }

        [HttpGet]
        public async Task<ActionResult<PackageListResponse>> GetPackagesAsync(
            [FromQuery] string? sort = null, 
            [FromQuery] string? search = null,
            [FromQuery] bool details = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                if (_testingModeService.IsTestingMode) Response.Headers.Add("X-Testing-Mode", "true");

                var searchResult = await _packageService.SearchPackagesAsync(search, sort, page, pageSize);
                
                var response = new PackageListResponse
                {
                    PackageCount = searchResult.TotalCount,
                    Packages = searchResult.Packages.Select(p => p.Name).ToList()
                };

                if (details)
                {
                    response.PackageDetails = searchResult.Packages.Select(p => new PurrConfig
                    {
                        Name = p.Name,
                        Version = p.Version,
                        Authors = JsonSerializer.Deserialize<List<string>>(p.Authors) ?? new List<string>(),
                        SupportedPlatforms = JsonSerializer.Deserialize<List<string>>(p.SupportedPlatforms) ?? new List<string>(),
                        Description = p.Description,
                        ReadmeUrl = p.ReadmeUrl,
                        License = p.License,
                        LicenseUrl = p.LicenseUrl,
                        Keywords = JsonSerializer.Deserialize<List<string>>(p.Keywords) ?? new List<string>(),
                        Categories = JsonSerializer.Deserialize<List<string>>(p.Categories) ?? new List<string>(),
                        Homepage = p.Homepage,
                        IssueTracker = p.IssueTracker,
                        Git = p.Git,
                        Installer = p.Installer,
                        Dependencies = JsonSerializer.Deserialize<List<string>>(p.Dependencies) ?? new List<string>(),
                        MainFile = p.MainFile,
                        IconUrl = p.IconUrl
                    }).ToList();
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting packages");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{packageName}/versions")]
        public async Task<ActionResult<List<string>>> GetPackageVersionsAsync(string packageName)
        {
            try
            {
                var versions = await _packageService.GetPackageVersionsAsync(packageName);
                if (versions.Count == 0)
                    return NotFound($"Package '{packageName}' not found");
                return Ok(versions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting versions for {PackageName}", Regex.Replace(packageName, _sanitizeRegex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{packageName}")]
        [HttpGet("{packageName}/{version}")]
        public async Task<ActionResult<PurrConfig>> GetPackageAsync(string packageName, string? version = null)
        {
            try
            {
                if (_testingModeService.IsTestingMode) Response.Headers.Add("X-Testing-Mode", "true");

                var package = await _packageService.GetPackageAsync(packageName, version);
                
                if (package == null)
                    return NotFound($"Package '{packageName}' not found");

                _ = _packageService.IncrementViewCountAsync(package.Id.ToString());

                return Ok(new PurrConfig
                {
                    Name = package.Name,
                    Version = package.Version,
                    Authors = JsonSerializer.Deserialize<List<string>>(package.Authors) ?? new List<string>(),
                    SupportedPlatforms = JsonSerializer.Deserialize<List<string>>(package.SupportedPlatforms) ?? new List<string>(),
                    Description = package.Description,
                    ReadmeUrl = package.ReadmeUrl,
                    License = package.License,
                    LicenseUrl = package.LicenseUrl,
                    Keywords = JsonSerializer.Deserialize<List<string>>(package.Keywords) ?? new List<string>(),
                    Categories = JsonSerializer.Deserialize<List<string>>(package.Categories) ?? new List<string>(),
                    Homepage = package.Homepage,
                    IssueTracker = package.IssueTracker,
                    Git = package.Git,
                    Installer = package.Installer,
                    Dependencies = JsonSerializer.Deserialize<List<string>>(package.Dependencies) ?? new List<string>(),
                    MainFile = package.MainFile,
                    IconUrl = package.IconUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting package {PackageName}", Regex.Replace(packageName, _sanitizeRegex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<PurrConfig>> UploadPackageAsync([FromBody] PurrConfig PurrConfig)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var userIdClaim = User.FindFirst("UserId");
                string? userId = userIdClaim?.Value;
                var userName = User.Identity?.Name ?? "api-user";
                var success = await _packageService.SavePackageAsync(PurrConfig, userName, userId);
                
                if (!success) return Conflict("Package already exists or failed to upload");

                return CreatedAtAction(nameof(GetPackageAsync), new { packageName = PurrConfig.Name }, PurrConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading package {PackageName}", Regex.Replace(PurrConfig.Name, _sanitizeRegex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{packageName}/download")]
        public async Task<ActionResult> IncrementDownloadAsync(string packageName)
        {
            try
            {
                var package = await _packageService.GetPackageAsync(packageName);
                if (package == null) return NotFound();

                await _packageService.IncrementDownloadCountAsync(package.Id.ToString());
                return Ok(new { message = "Download count incremented" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing download count for {PackageName}", Regex.Replace(packageName, _sanitizeRegex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<PackageStatistics>> GetStatisticsAsync()
        {
            try
            {
                return Ok(await _packageService.GetStatisticsAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("tags")]
        public async Task<ActionResult<List<string>>> GetPopularTagsAsync([FromQuery] int limit = 10) => Ok(await _packageService.GetPopularTagsAsync(limit));

        [HttpGet("authors")]
        public async Task<ActionResult<List<string>>> GetPopularAuthorsAsync([FromQuery] int limit = 10) => Ok(await _packageService.GetPopularAuthorsAsync(limit));

        [HttpGet("categories")]
        public async Task<ActionResult<List<string>>> GetPopularCategoriesAsync([FromQuery] int limit = 10) => Ok(await _packageService.GetPopularCategoriesAsync(limit));

        [HttpGet("tags/{tag}")]
        public async Task<ActionResult<List<Package>>> GetPackagesByTagAsync(string tag) => Ok(await _packageService.GetPackagesByTagAsync(tag));

        [HttpGet("authors/{author}")]
        public async Task<ActionResult<List<Package>>> GetPackagesByAuthorAsync(string author) => Ok(await _packageService.GetPackagesByAuthorAsync(author));

        [HttpGet("categories/{category}")]
        public async Task<ActionResult<List<Package>>> GetPackagesByCategoryAsync(string category) => Ok(await _packageService.GetPackagesByCategoryAsync(category));

        [HttpGet("{packageName}/reviews")]
        public async Task<ActionResult<List<PackageReview>>> GetReviewsAsync(string packageName)
        {
            var reviews = await _packageService.GetPackageReviewsAsync(packageName);
            return Ok(reviews.Select(r => new { r.Id, r.Rating, r.Title, r.Body, r.ReviewerName, r.ReviewerAvatarUrl, r.CreatedAt, r.UserId }));
        }

        [HttpPost("{packageName}/reviews")]
        public async Task<ActionResult> SubmitReviewAsync(string packageName, [FromBody] SubmitReviewRequest request)
        {
            var userId = User.FindFirst("UserId")?.Value;
            var reviewerName = User.Identity?.Name ?? "Anonymous";
            var (success, error) = await _packageService.AddPackageReviewAsync(packageName, userId, reviewerName, null, request.Rating, request.Title ?? string.Empty, request.Body);
            return success ? Ok() : BadRequest(new { error });
        }

        [HttpDelete("{packageName}/reviews/{reviewId}")]
        [Authorize]
        public async Task<ActionResult> DeleteReviewAsync(string packageName, string reviewId)
        {
            var userId = User.FindFirst("UserId")?.Value;
            bool isAdmin = User.IsInRole("Admin") || (User.FindFirst("IsAdmin")?.Value == "true");
            var deleted = await _packageService.DeleteReviewAsync(reviewId, userId, isAdmin);
            return deleted ? Ok() : Forbid();
        }

        [HttpGet("{packageName}/deptree")]
        public async Task<ActionResult<DependencyNode>> GetDependencyTreeAsync(string packageName, [FromQuery] int depth = 3)
        {
            var tree = await _packageService.GetDependencyTreeAsync(packageName, Math.Clamp(depth, 1, 5));
            return tree == null ? NotFound() : Ok(tree);
        }

        [HttpDelete("cache")]
        public ActionResult ClearCache() => Ok();

        [HttpGet("export/purrconfigs")]
        public async Task<IActionResult> ExportPurrConfigs()
        {
            var packages = await _packageService.GetAllPackagesAsync();
            using var mem = new MemoryStream();
            using (var archive = new ZipArchive(mem, ZipArchiveMode.Create, true))
            {
                foreach (var pkg in packages)
                {
                    var purr = new PurrConfig
                    {
                        Name = pkg.Name,
                        Version = pkg.Version,
                        Authors = JsonSerializer.Deserialize<List<string>>(pkg.Authors) ?? new List<string>(),
                        SupportedPlatforms = JsonSerializer.Deserialize<List<string>>(pkg.SupportedPlatforms) ?? new List<string>(),
                        Description = pkg.Description,
                        ReadmeUrl = pkg.ReadmeUrl,
                        License = pkg.License,
                        LicenseUrl = pkg.LicenseUrl,
                        Keywords = JsonSerializer.Deserialize<List<string>>(pkg.Keywords) ?? new List<string>(),
                        Categories = JsonSerializer.Deserialize<List<string>>(pkg.Categories) ?? new List<string>(),
                        Homepage = pkg.Homepage,
                        IssueTracker = pkg.IssueTracker,
                        Git = pkg.Git,
                        Installer = pkg.Installer,
                        Dependencies = JsonSerializer.Deserialize<List<string>>(pkg.Dependencies) ?? new List<string>(),
                        IconUrl = pkg.IconUrl
                    };
                    var entry = archive.CreateEntry(SanitizeFileName(pkg.Name) + ".Purrconfig.json");
                    using var es = entry.Open();
                    using var sw = new StreamWriter(es);
                    sw.Write(JsonSerializer.Serialize(purr));
                }
            }
            return File(mem.ToArray(), "application/zip", "purrconfigs.zip");
        }

        [HttpPost("import/purrconfigs")]
        [Authorize]
        public async Task<IActionResult> ImportPurrConfigs(IFormFile file) => Ok();

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}
