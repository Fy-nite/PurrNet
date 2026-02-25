using System.Text.Json.Serialization;

namespace PurrLauncher.Models;

public class PackageListResult
{
    [JsonPropertyName("package_count")]
    public int PackageCount { get; set; }

    [JsonPropertyName("packages")]
    public string[] Packages { get; set; } = Array.Empty<string>();

    [JsonPropertyName("package_details")]
    public PackageInfo[] DetailedPackages { get; set; } = Array.Empty<PackageInfo>();
}
