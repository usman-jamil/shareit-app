namespace AppImage.Host.Configuration;

/// <summary>
/// Everything the app image host needs to know at startup. Bound once from the
/// <c>AppImage</c> configuration section (see <see cref="EnvironmentConfiguration"/> for the
/// <c>APP_IMAGE_*</c> environment-variable aliases) and validated before the server starts.
/// </summary>
public sealed class AppImageOptions
{
    public const string SectionName = "AppImage";

    /// <summary>
    /// Directory holding the built React application. Must contain <c>index.html</c>.
    /// </summary>
    public string WebRoot { get; set; } = "/app/web";

    public ApiProxyOptions Api { get; set; } = new();

    public ForwardedHeadersSettings ForwardedHeaders { get; set; } = new();
}

/// <summary>
/// How the host reaches the API process that runs alongside it in the container.
/// </summary>
public sealed class ApiProxyOptions
{
    /// <summary>
    /// Base address of the internal API. Loopback by default: the API is not published by Docker.
    /// </summary>
    public string Destination { get; set; } = "http://127.0.0.1:5000/";

    /// <summary>
    /// Public path prefix that identifies API traffic. Requests to this prefix are proxied and
    /// are never allowed to fall through to the SPA document.
    /// </summary>
    public string PathPrefix { get; set; } = "/api";

    /// <summary>
    /// Whether <see cref="PathPrefix"/> is removed before forwarding.
    /// <para>
    /// Defaults to <see langword="true"/> because this repository's API maps its endpoints at the
    /// root of its own address space — <c>apps/api/Program.cs</c> calls <c>MapEndpoints()</c> with
    /// no route group, so the routes are <c>/shares</c>, <c>/users/{id}</c> and <c>/health</c>.
    /// The <c>/api</c> prefix exists only in the public URL space this host owns. If the API ever
    /// moves its endpoints under <c>/api</c>, set this to <see langword="false"/>.
    /// </para>
    /// </summary>
    public bool StripPathPrefix { get; set; } = true;

    /// <summary>
    /// Path on the API, relative to <see cref="Destination"/>, used by the readiness probe.
    /// </summary>
    public string HealthPath { get; set; } = "/health";

    /// <summary>
    /// How long the readiness probe waits for the API before calling it unreachable.
    /// </summary>
    public int HealthTimeoutSeconds { get; set; } = 5;
}

/// <summary>
/// Controls how much this host trusts <c>X-Forwarded-*</c> headers from whatever sits in front of it.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    /// <summary>
    /// When <see langword="false"/> (the default) only loopback proxies are trusted, which is
    /// ASP.NET Core's own default. Set to <see langword="true"/> when the container runs behind a
    /// trusted ingress on a container network, so client IP and scheme survive the hop.
    /// </summary>
    public bool TrustAllProxies { get; set; }
}
