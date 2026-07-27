namespace Share.Domain.Configuration;

/// <summary>
/// Values used when the configuration file does not set them. The single source of
/// defaults — both the bound options and <c>config show</c> read from here, so there is
/// no second copy to drift.
/// </summary>
public static class ShareApiDefaults
{
    public const int TimeoutSeconds = 100;

    private const string LocalScheme = "http";
    private const string LocalHost = "localhost";
    private const int LocalPort = 5080;

    /// <summary>
    /// The locally served API, matching the port <c>nx serve api</c> listens on.
    /// </summary>
    public static Uri BaseUrl { get; } = new UriBuilder(LocalScheme, LocalHost, LocalPort).Uri;
}
