using System.Text.Json.Serialization;

namespace PurrLauncher.Models;

public class PackageInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("authors")]
    public string[] Authors { get; set; } = Array.Empty<string>();

    [JsonPropertyName("homepage")]
    public string Homepage { get; set; } = string.Empty;

    [JsonPropertyName("issue_tracker")]
    public string IssueTracker { get; set; } = string.Empty;

    [JsonPropertyName("git")]
    public string Git { get; set; } = string.Empty;

    [JsonPropertyName("installer")]
    public string Installer { get; set; } = string.Empty;

    [JsonPropertyName("dependencies")]
    public string[] Dependencies { get; set; } = Array.Empty<string>();

    [JsonPropertyName("categories")]
    public string[] Categories { get; set; } = Array.Empty<string>();

    [JsonPropertyName("mainfile")]
    public string? MainFile { get; set; }

    // Computed display helpers
    public string AuthorsDisplay =>
        Authors.Length > 0 ? string.Join(", ", Authors) : "Unknown";

    public string CategoriesDisplay =>
        Categories.Length > 0 ? string.Join(", ", Categories) : "None";

    public string DependenciesDisplay =>
        Dependencies.Length > 0 ? string.Join(", ", Dependencies) : "None";

    /// <summary>Constructs a GitHub releases URL from the Git field, or falls back to Homepage.</summary>
    public string ReleasesUrl
    {
        get
        {
            if (!string.IsNullOrEmpty(Git))
            {
                var url = Git.TrimEnd('/');
                if (url.Contains("github.com"))
                    return $"{url}/releases/latest";
            }
            return !string.IsNullOrEmpty(Homepage) ? Homepage : string.Empty;
        }
    }

    public bool HasHomepage => !string.IsNullOrEmpty(Homepage);
    public bool HasGit => !string.IsNullOrEmpty(Git);

    /// <summary>
    /// Set at runtime by the ViewModel on Android to flag packages that are
    /// desktop-only and may not work on mobile.
    /// </summary>
    [JsonIgnore]
    public bool IsDesktopWarning { get; set; }
}
