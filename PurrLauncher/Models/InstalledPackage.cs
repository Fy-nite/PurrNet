namespace PurrLauncher.Models;

public class InstalledPackage
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public DateTime InstalledAt { get; set; }

    public string InstalledAtDisplay =>
        InstalledAt == default ? "Unknown" : InstalledAt.ToString("MMM d, yyyy");
}
