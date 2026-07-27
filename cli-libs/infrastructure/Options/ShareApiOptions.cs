using Share.Domain.Configuration;

namespace Share.Infrastructure.Options;

/// <summary>
/// How to reach the Share API. Bound from the <c>ShareApi</c> configuration section, whose
/// source of truth is the user's <c>~/.share/config.yaml</c>.
/// </summary>
public sealed class ShareApiOptions
{
    public const string SectionName = "ShareApi";

    /// <summary>
    /// Root address of the API, e.g. <c>https://api.example.com</c>.
    /// </summary>
    public Uri BaseUrl { get; set; } = ShareApiDefaults.BaseUrl;

    /// <summary>
    /// API key sent as the <c>X-Api-Key</c> header. Deliberately not validated at
    /// startup: a CLI must still run <c>--help</c> without configuration, so a missing or
    /// wrong key surfaces as a normal failure result when a command actually calls the API.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Per-request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = ShareApiDefaults.TimeoutSeconds;
}
