using System.Text.Json.Serialization;

namespace Fur.Models;

public class FurConfig
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
}
