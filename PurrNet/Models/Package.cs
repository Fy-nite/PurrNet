using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Purrnet.Models
{
    public class Package
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string Version { get; set; } = string.Empty;
        
        public string Authors { get; set; } = "[]"; 
        
        public string SupportedPlatforms { get; set; } = "[]";
        
        public string Description { get; set; } = string.Empty;
        
        public string ReadmeUrl { get; set; } = string.Empty;
        
        public string License { get; set; } = string.Empty;
        
        public string LicenseUrl { get; set; } = string.Empty;
        
        public string Keywords { get; set; } = "[]";
        
        public string Categories { get; set; } = "[]";

        [NotMapped]
        public List<Category> CategoryEntities { get; set; } = new();
        
        public string Homepage { get; set; } = string.Empty;
        
        public string IssueTracker { get; set; } = string.Empty;
        
        [Required]
        public string Git { get; set; } = string.Empty;
        
        public string Installer { get; set; } = string.Empty;
        
        [Required]
        public string InstallCommand { get; set; } = string.Empty;
        
        public string Dependencies { get; set; } = "[]";
        
        public int Downloads { get; set; }
        public string LastUpdated { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public int IsActive { get; set; }
        public int IsOutdated { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public int ViewCount { get; set; }
        public double Rating { get; set; }
        public int RatingCount { get; set; }
        public int SizeInBytes { get; set; }
        public string? Readme { get; set; }
        public string? Changelog { get; set; }
        public string IconUrl { get; set; } = string.Empty;
        public string VersionHistory { get; set; } = "[]";

        public string? MainFile { get; set; }

        public string ApprovalStatus { get; set; } = "Pending"; 
        public int? OwnerId { get; set; }
        public string? RejectionReason { get; set; }
        public int IsLibrary { get; set; }

        [NotMapped]
        public User? Owner { get; set; }
        
        [NotMapped]
        public List<User> Maintainers { get; set; } = new();
        
        [NotMapped]
        public List<PackageReview> Reviews { get; set; } = new();

        // ── Helpers for legacy schema ─────────────────────────────────────────────
        
        [NotMapped, JsonIgnore]
        public List<string> AuthorsList => SafeDeserialize(Authors);
        
        [NotMapped, JsonIgnore]
        public List<string> SupportedPlatformsList => SafeDeserialize(SupportedPlatforms);
        
        [NotMapped, JsonIgnore]
        public List<string> KeywordsList => SafeDeserialize(Keywords);
        
        [NotMapped, JsonIgnore]
        public List<string> CategoriesList => SafeDeserialize(Categories);
        
        [NotMapped, JsonIgnore]
        public List<string> DependenciesList => SafeDeserialize(Dependencies);
        
        [NotMapped, JsonIgnore]
        public List<string> VersionHistoryList => SafeDeserialize(VersionHistory);

        [NotMapped, JsonIgnore]
        public DateTime CreatedAtDateTime => DateTime.TryParse(CreatedAt, out var dt) ? dt : DateTime.MinValue;

        [NotMapped, JsonIgnore]
        public DateTime LastUpdatedDateTime => DateTime.TryParse(LastUpdated, out var dt) ? dt : DateTime.MinValue;

        private static List<string> SafeDeserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "[]") return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
            catch { return new List<string>(); }
        }
    }

    public class PackageReview
    {
        [Key]
        public int Id { get; set; }
        
        public int PackageId { get; set; }
        public int? UserId { get; set; }

        public int Rating { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerAvatarUrl { get; set; } = string.Empty;

        public string CreatedAt { get; set; } = string.Empty;
        public string? UpdatedAt { get; set; }

        [NotMapped, JsonIgnore]
        public DateTime CreatedAtDateTime => DateTime.TryParse(CreatedAt, out var dt) ? dt : DateTime.MinValue;

        [NotMapped]
        public Package? Package { get; set; }
        
        [NotMapped]
        public User? User { get; set; }
    }


    public class DependencyNode
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<DependencyNode> Dependencies { get; set; } = new();
        public bool Resolved { get; set; }
    }

    public class SubmitReviewRequest
    {
        public int Rating { get; set; }
        public string? Title { get; set; }
        public string Body { get; set; } = string.Empty;
    }

    public class PurrConfig
    {
        [JsonPropertyName("name")]
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("version")]
        [Required]
        public string Version { get; set; } = string.Empty;
        
        [JsonPropertyName("authors")]
        [Required]
        public List<string> Authors { get; set; } = new();
        
        [JsonPropertyName("Supported_Platforms")]
        public List<string> SupportedPlatforms { get; set; } = new();
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("readme_url")]
        public string ReadmeUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("license")]
        public string License { get; set; } = string.Empty;
        
        [JsonPropertyName("license_url")]
        public string LicenseUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new();
        
        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new();
        
        [JsonPropertyName("homepage")]
        public string Homepage { get; set; } = string.Empty;
        
        [JsonPropertyName("issue_tracker")]
        public string IssueTracker { get; set; } = string.Empty;
        
        [JsonPropertyName("git")]
        [Required]
        public string Git { get; set; } = string.Empty;
        
        [JsonPropertyName("installer")]
        public string Installer { get; set; } = string.Empty;
        
        [JsonPropertyName("dependencies")]
        public List<string> Dependencies { get; set; } = new();

        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("mainfile")]
        [Required]
        public string MainFile { get; set; } = string.Empty;

        [JsonPropertyName("is_library")]
        public bool IsLibrary { get; set; }
    }

    public class PackageListResponse
    {
        [JsonPropertyName("package_count")]
        public int PackageCount { get; set; }
        
        [JsonPropertyName("packages")]
        public List<string> Packages { get; set; } = new();
        
        [JsonPropertyName("package_details")]
        public List<PurrConfig>? PackageDetails { get; set; }
    }

    public class PackageStatistics
    {
        public int TotalPackages { get; set; }
        public int ActivePackages { get; set; }
        public int TotalDownloads { get; set; }
        public int TotalViews { get; set; }
        public int ActiveUsers { get; set; }
        public List<string> PopularAuthors { get; set; } = new();
        public List<Package> MostDownloaded { get; set; } = new();
        public List<Package> RecentlyAdded { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class SearchResult
    {
        public List<Package> Packages { get; set; } = new();
        public int TotalCount { get; set; }
        public string Query { get; set; } = string.Empty;
        public List<string> SuggestedAuthors { get; set; } = new();
    }

    public class User
    {
        [Key]
        public int Id { get; set; }
        public int GitHubId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string LastLoginAt { get; set; } = string.Empty;
        public int IsAdmin { get; set; }
        public int IsBanned { get; set; } = 0;

        [NotMapped, JsonIgnore]
        public DateTime CreatedAtDateTime => DateTime.TryParse(CreatedAt, out var dt) ? dt : DateTime.MinValue;

        [NotMapped, JsonIgnore]
        public DateTime LastLoginAtDateTime => DateTime.TryParse(LastLoginAt, out var dt) ? dt : DateTime.MinValue;

        [NotMapped]
        public List<Package> OwnedPackages { get; set; } = new();
        
        [NotMapped]
        public List<Package> MaintainedPackages { get; set; } = new();
        
        [NotMapped]
        public List<AdminActivityEntity> AdminActivities { get; set; } = new();
        [NotMapped]
        public List<PackageReview> Reviews { get; set; } = new();
    }

    public class AdminActivity
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public class AdminActivityEntity
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");

        [NotMapped]
        public User User { get; set; } = null!;
    }
}
