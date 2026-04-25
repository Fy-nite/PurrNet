namespace PurrLauncher.Models;

/// <summary>A named package repository endpoint.</summary>
public sealed class RepoEntry
{
    public string Name { get; set; } = string.Empty;
    public string Url  { get; set; } = string.Empty;

    public RepoEntry() { }
    public RepoEntry(string name, string url) { Name = name; Url = url; }

    public override string ToString() => $"{Name}|{Url}";

    /// <summary>Parses the <see cref="ToString"/> format. Returns null on failure.</summary>
    public static RepoEntry? TryParse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var idx = raw.IndexOf('|');
        if (idx < 1) return null;
        return new RepoEntry(raw[..idx].Trim(), raw[(idx + 1)..].Trim());
    }
}
