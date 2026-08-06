using AppImage.Host.Configuration;
using Yarp.ReverseProxy.Configuration;

namespace AppImage.Host.Proxy;

/// <summary>
/// Builds the YARP route and cluster configuration from the validated options.
/// <para>
/// Held in code rather than in <c>appsettings.json</c> so the destination, the path prefix and the
/// prefix-stripping rule all come from one validated source, and so a typo in a config file cannot
/// silently produce a proxy with no routes.
/// </para>
/// </summary>
internal static class ProxyConfiguration
{
    private const string ClusterId = "internal-api";
    private const string DestinationId = "primary";

    public static IReadOnlyList<RouteConfig> CreateRoutes(ValidatedAppImageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IReadOnlyList<IReadOnlyDictionary<string, string>>? transforms = options.StripApiPathPrefix
            ? [new Dictionary<string, string>(StringComparer.Ordinal) { ["PathRemovePrefix"] = options.ApiPathPrefix }]
            : null;

        return
        [
            // Both are needed: "/api/{**catch-all}" does not match a bare "/api".
            new RouteConfig
            {
                RouteId = "api-root",
                ClusterId = ClusterId,
                Match = new RouteMatch { Path = options.ApiPathPrefix },
                Transforms = transforms,
            },
            new RouteConfig
            {
                RouteId = "api",
                ClusterId = ClusterId,
                Match = new RouteMatch { Path = $"{options.ApiPathPrefix}/{{**catch-all}}" },
                Transforms = transforms,
            },
        ];
    }

    public static IReadOnlyList<ClusterConfig> CreateClusters(ValidatedAppImageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return
        [
            new ClusterConfig
            {
                ClusterId = ClusterId,
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal)
                {
                    [DestinationId] = new DestinationConfig { Address = options.ApiDestination.ToString() },
                },
            },
        ];
    }
}
