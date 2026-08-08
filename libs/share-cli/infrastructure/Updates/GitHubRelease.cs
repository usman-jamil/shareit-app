using System.Text.Json.Serialization;

namespace Share.Infrastructure.Updates;

/// <summary>
/// The fields of GitHub's release representation that the CLI reads. Everything else in
/// the payload is ignored, so a field GitHub adds cannot break the deserializer.
/// </summary>
internal sealed record GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; init; }

    [JsonPropertyName("draft")]
    public bool Draft { get; init; }

    [JsonPropertyName("prerelease")]
    public bool PreRelease { get; init; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("assets")]
    public IReadOnlyList<GitHubReleaseAsset>? Assets { get; init; }
}

internal sealed record GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; init; }

    [JsonPropertyName("size")]
    public long Size { get; init; }
}
