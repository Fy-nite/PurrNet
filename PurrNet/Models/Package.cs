using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Purrnet.Models
{
    public class Package
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string Version { get; set; } = string.Empty;
        
        [Required]
        public List<string> Authors { get; set; } = new();
        
        public List<string> SupportedPlatforms { get; set; } = new();
        
        public string Description { get; set; } = string.Empty;
        
        public string ReadmeUrl { get; set; } = string.Empty;
        
        public string License { get; set; } = string.Empty;
        
        public string LicenseUrl { get; set; } = string.Empty;
        
        public List<string> Keywords { get; set; } = new();
        
        // Add categories support
        public List<string> Categories { get; set; } = new();

        // Navigation property for relational categories (many-to-many)
        public List<Category> CategoryEntities { get; set; } = new();
        
        public string Homepage { get; set; } = string.Empty;
        
        public string IssueTracker { get; set; } = string.Empty;
        
        [Required]
        public string Git { get; set; } = string.Empty;
        
        public string Installer { get; set; } = string.Empty;
        
        [Required]
        public string InstallCommand { get; set; } = string.Empty;
        
        public List<string> Dependencies { get; set; } = new();
        
        // Additional metadata for the website
        public int Downloads { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public int ViewCount { get; set; }
        public double Rating { get; set; }
        public int RatingCount { get; set; }
        public long SizeInBytes { get; set; }
        public string? Readme { get; set; }
        public string? Changelog { get; set; }
        public string IconUrl { get; set; } = string.Empty;
        public List<string> VersionHistory { get; set; } = new();

        // Package approval system
        public string ApprovalStatus { get; set; } = "Pending"; // Default to Approved for backward compatibility
        public int? OwnerId { get; set; }
        public string? RejectionReason { get; set; }

        // Navigation properties
        public User? Owner { get; set; }
        public List<User> Maintainers { get; set; } = new();
        public List<PackageReview> Reviews { get; set; } = new();
    }

    public class PackageReview
    {
        public int Id { get; set; }
        public int PackageId { get; set; }
        public int? UserId { get; set; }

        /// <summary>1-5 star rating</summary>
        public int Rating { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        /// <summary>Display name – copied at review time so it survives user renames</summary>
        public string ReviewerName { get; set; } = string.Empty;
        public string? ReviewerAvatarUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public Package? Package { get; set; }
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
        
        // Add categories support to PurrConfig
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
        public int Id { get; set; }
        public string GitHubId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
        public bool IsAdmin { get; set; }

        // Navigation properties
        public List<Package> OwnedPackages { get; set; } = new();
        public List<Package> MaintainedPackages { get; set; } = new();
        public List<AdminActivityEntity> AdminActivities { get; set; } = new();
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
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User User { get; set; } = null!;
    }
}
