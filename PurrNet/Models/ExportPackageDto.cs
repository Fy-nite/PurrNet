using System.Text.Json.Serialization;

namespace Purrnet.Models
{
    public class OwnerInfo
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("github_id")]
        public string? GitHubId { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    public class ExportPackageDto
    {
        [JsonPropertyName("purrconfig")]
        public PurrConfig PurrConfig { get; set; } = new();

        [JsonPropertyName("owner")]
        public OwnerInfo? Owner { get; set; }

        [JsonPropertyName("package_id")]
        public int? PackageId { get; set; }
    }
}
