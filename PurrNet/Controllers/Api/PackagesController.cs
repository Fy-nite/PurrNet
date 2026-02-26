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
        private readonly string _sanitize_regex = @"[^\x20-\x7e]+";

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
                if (_testingModeService.IsTestingMode)
                {
                    Response.Headers.Add("X-Testing-Mode", "true");
                }

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
                        Authors = p.Authors,
                        SupportedPlatforms = p.SupportedPlatforms,
                        Description = p.Description,
                        ReadmeUrl = p.ReadmeUrl,
                        License = p.License,
                        LicenseUrl = p.LicenseUrl,
                        Keywords = p.Keywords,
                        Categories = p.Categories,
                        Homepage = p.Homepage,
                        IssueTracker = p.IssueTracker,
                        Git = p.Git,
                        Installer = p.Installer,
                        Dependencies = p.Dependencies,
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
                _logger.LogError(ex, "Error getting versions for {PackageName}", Regex.Replace(packageName, _sanitize_regex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{packageName}")]
        [HttpGet("{packageName}/{version}")]
        public async Task<ActionResult<PurrConfig>> GetPackageAsync(string packageName, string? version = null)
        {
            try
            {
                if (_testingModeService.IsTestingMode)
                {
                    Response.Headers.Add("X-Testing-Mode", "true");
                }

                var package = await _packageService.GetPackageAsync(packageName, version);
                
                if (package == null)
                    return NotFound($"Package '{packageName}' not found");

                // Increment view count in the background so it doesn't delay the response
                _ = _packageService.IncrementViewCountAsync(package.Id);

                var PurrConfig = new PurrConfig
                {
                    Name = package.Name,
                    Version = package.Version,
                    Authors = package.Authors,
                    SupportedPlatforms = package.SupportedPlatforms,
                    Description = package.Description,
                    ReadmeUrl = package.ReadmeUrl,
                    License = package.License,
                    LicenseUrl = package.LicenseUrl,
                    Keywords = package.Keywords,
                    Categories = package.Categories,
                    Homepage = package.Homepage,
                    IssueTracker = package.IssueTracker,
                    Git = package.Git,
                    Installer = package.Installer,
                    Dependencies = package.Dependencies,
                    MainFile = package.MainFile,
                    IconUrl = package.IconUrl
                };

                return Ok(PurrConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting package {PackageName}", Regex.Replace(packageName, _sanitize_regex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<PurrConfig>> UploadPackageAsync([FromBody] PurrConfig PurrConfig)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                if (_testingModeService.IsTestingMode)
                {
                    Response.Headers.Add("X-Testing-Mode", "true");
                }

                // Get user info from authentication
                var userIdClaim = User.FindFirst("UserId");
                int? userId = null;
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var parsedUserId))
                {
                    userId = parsedUserId;
                }

                var userName = User.Identity?.Name ?? "api-user";
                var success = await _packageService.SavePackageAsync(PurrConfig, userName, userId);
                
                if (!success)
                    return Conflict("Package already exists or failed to upload");

                return CreatedAtAction(nameof(GetPackageAsync), new { packageName = PurrConfig.Name }, PurrConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading package {PackageName}", Regex.Replace(PurrConfig.Name, _sanitize_regex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{packageName}/download")]
        public async Task<ActionResult> IncrementDownloadAsync(string packageName)
        {
            try
            {
                var package = await _packageService.GetPackageAsync(packageName);
                if (package == null)
                    return NotFound();

                await _packageService.IncrementDownloadCountAsync(package.Id);
                return Ok(new { message = "Download count incremented" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing download count for {PackageName}", Regex.Replace(packageName, _sanitize_regex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<PackageStatistics>> GetStatisticsAsync()
        {
            try
            {
                if (_testingModeService.IsTestingMode)
                {
                    Response.Headers.Add("X-Testing-Mode", "true");
                }

                var stats = await _packageService.GetStatisticsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("tags")]
        public async Task<ActionResult<List<string>>> GetPopularTagsAsync([FromQuery] int limit = 10)
        {
            try
            {
                var tags = await _packageService.GetPopularTagsAsync(limit);
                return Ok(tags);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular tags");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("authors")]
        public async Task<ActionResult<List<string>>> GetPopularAuthorsAsync([FromQuery] int limit = 10)
        {
            try
            {
                var authors = await _packageService.GetPopularAuthorsAsync(limit);
                return Ok(authors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular authors");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("categories")]
        public async Task<ActionResult<List<string>>> GetPopularCategoriesAsync([FromQuery] int limit = 10)
        {
            try
            {
                var categories = await _packageService.GetPopularCategoriesAsync(limit);
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular categories");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("tags/{tag}")]
        public async Task<ActionResult<List<Package>>> GetPackagesByTagAsync(string tag)
        {
            try
            {
                var packages = await _packageService.GetPackagesByTagAsync(tag);
                return Ok(packages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting packages by tag {Tag}", Regex.Replace(tag, _sanitize_regex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("authors/{author}")]
        public async Task<ActionResult<List<Package>>> GetPackagesByAuthorAsync(string author)
        {
            try
            {
                var packages = await _packageService.GetPackagesByAuthorAsync(author);
                return Ok(packages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting packages by author {Author}", Regex.Replace(author, _sanitize_regex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("categories/{category}")]
        public async Task<ActionResult<List<Package>>> GetPackagesByCategoryAsync(string category)
        {
            try
            {
                var packages = await _packageService.GetPackagesByCategoryAsync(category);
                return Ok(packages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting packages by category {Category}", Regex.Replace(category, _sanitize_regex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{packageName}/reviews")]
        public async Task<ActionResult<List<PackageReview>>> GetReviewsAsync(string packageName)
        {
            try
            {
                var reviews = await _packageService.GetPackageReviewsAsync(packageName);
                return Ok(reviews.Select(r => new
                {
                    r.Id,
                    r.Rating,
                    r.Title,
                    r.Body,
                    r.ReviewerName,
                    r.ReviewerAvatarUrl,
                    r.CreatedAt,
                    r.UserId
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reviews for {PackageName}", Regex.Replace(packageName, _sanitize_regex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("{packageName}/reviews")]
        public async Task<ActionResult> SubmitReviewAsync(string packageName, [FromBody] SubmitReviewRequest request)
        {
            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest("Rating must be between 1 and 5.");
            if (string.IsNullOrWhiteSpace(request.Body))
                return BadRequest("Review body is required.");

            var userIdClaim = User.FindFirst("UserId");
            int? userId = null;
            string reviewerName = "Anonymous";
            string? reviewerAvatarUrl = null;

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var parsedId))
            {
                userId = parsedId;
                reviewerName = User.Identity?.Name ?? "User";
            }

            var (success, error) = await _packageService.AddPackageReviewAsync(
                packageName, userId, reviewerName, reviewerAvatarUrl,
                request.Rating, request.Title ?? string.Empty, request.Body);

            if (!success)
                return BadRequest(new { error });

            return Ok(new { message = "Review submitted successfully." });
        }

        [HttpDelete("{packageName}/reviews/{reviewId:int}")]
        [Authorize]
        public async Task<ActionResult> DeleteReviewAsync(string packageName, int reviewId)
        {
            var userIdClaim = User.FindFirst("UserId");
            int? userId = null;
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var parsedId))
                userId = parsedId;

            bool isAdmin = User.IsInRole("Admin") || (User.FindFirst("IsAdmin")?.Value == "true");

            var deleted = await _packageService.DeleteReviewAsync(reviewId, userId, isAdmin);
            return deleted ? Ok() : Forbid();
        }

        [HttpGet("{packageName}/deptree")]
        public async Task<ActionResult<DependencyNode>> GetDependencyTreeAsync(
            string packageName, [FromQuery] int depth = 3)
        {
            try
            {
                depth = Math.Clamp(depth, 1, 5);
                var tree = await _packageService.GetDependencyTreeAsync(packageName, depth);
                if (tree == null)
                    return NotFound($"Package '{packageName}' not found");
                return Ok(tree);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building dependency tree for {PackageName}", Regex.Replace(packageName, _sanitize_regex, ""));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("cache")]
        public ActionResult ClearCache()
        {
            // For compatibility with package managers expecting this endpoint
            return Ok(new { message = "Cache cleared (using database storage)" });
        }

        [HttpGet("export/purrconfigs")]
        public async Task<IActionResult> ExportPurrConfigs()
        {
            try
            {
                var packages = await _packageService.GetAllPackagesAsync();

                using var mem = new MemoryStream();
                using (var archive = new ZipArchive(mem, ZipArchiveMode.Create, true))
                {
                    var owners = new Dictionary<string, object?>();

                    foreach (var pkg in packages)
                    {
                        var purr = new PurrConfig
                        {
                            Name = pkg.Name,
                            Version = pkg.Version,
                            Authors = pkg.Authors,
                            SupportedPlatforms = pkg.SupportedPlatforms,
                            Description = pkg.Description,
                            ReadmeUrl = pkg.ReadmeUrl,
                            License = pkg.License,
                            LicenseUrl = pkg.LicenseUrl,
                            Keywords = pkg.Keywords,
                            Categories = pkg.Categories,
                            Homepage = pkg.Homepage,
                            IssueTracker = pkg.IssueTracker,
                            Git = pkg.Git,
                            Installer = pkg.Installer,
                            Dependencies = pkg.Dependencies,
                            IconUrl = pkg.IconUrl
                        };

                        var json = JsonSerializer.Serialize(purr, new JsonSerializerOptions { WriteIndented = true });
                        var entry = archive.CreateEntry(SanitizeFileName(pkg.Name) + ".Purrconfig.json");
                        using var es = entry.Open();
                        using var sw = new StreamWriter(es);
                        sw.Write(json);

                        owners[pkg.Name] = new { pkg.CreatedBy, pkg.OwnerId };
                    }

                    // Add owners manifest
                    var ownersEntry = archive.CreateEntry("owners.json");
                    using (var es = ownersEntry.Open())
                    using (var sw = new StreamWriter(es))
                    {
                        sw.Write(JsonSerializer.Serialize(owners, new JsonSerializerOptions { WriteIndented = true }));
                    }
                }

                mem.Seek(0, SeekOrigin.Begin);
                return File(mem.ToArray(), "application/zip", "purrconfigs.zip");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Purrconfig files");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("import/purrconfigs")]
        [Authorize]
        public async Task<IActionResult> ImportPurrConfigs(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                Dictionary<string, JsonElement>? owners = null;

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);

                using var archive = new ZipArchive(ms, ZipArchiveMode.Read, true);
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.Equals("owners.json", StringComparison.OrdinalIgnoreCase))
                    {
                        using var es = entry.Open();
                        using var sr = new StreamReader(es);
                        var content = await sr.ReadToEndAsync();
                        owners = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
                        continue;
                    }

                    if (!entry.FullName.EndsWith(".Purrconfig.json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    using var es2 = entry.Open();
                    using var sr2 = new StreamReader(es2);
                    var json = await sr2.ReadToEndAsync();
                    var purr = JsonSerializer.Deserialize<PurrConfig>(json);
                    if (purr == null) continue;

                    int? ownerId = null;
                    string createdBy = "import";

                    if (owners != null && owners.TryGetValue(purr.Name, out var ownerElem))
                    {
                        try
                        {
                            if (ownerElem.TryGetProperty("OwnerId", out var oid) && oid.ValueKind == JsonValueKind.Number)
                                ownerId = oid.GetInt32();

                            if (ownerElem.TryGetProperty("CreatedBy", out var cb) && cb.ValueKind == JsonValueKind.String)
                                createdBy = cb.GetString() ?? createdBy;
                        }
                        catch { }
                    }

                    await _packageService.SavePackageAsync(purr, createdBy, ownerId);
                }

                return Ok(new { message = "Import complete" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing Purrconfig files");
                return StatusCode(500, "Internal server error");
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
