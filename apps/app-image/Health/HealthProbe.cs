using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace AppImage.Host.Health;

/// <summary>
/// The container's <c>HEALTHCHECK</c> command, implemented as an argument to this same executable.
/// <para>
/// The ASP.NET Core runtime image ships neither <c>curl</c> nor <c>wget</c>, and adding either
/// means an <c>apt-get</c> layer and its ongoing CVE surface for the sake of one HTTP GET. The
/// .NET runtime and this assembly are already in the image, so <c>dotnet AppImage.Host.dll
/// --healthcheck</c> costs nothing extra to ship. It runs before the web host is built, so it never
/// tries to bind a port.
/// </para>
/// </summary>
internal static class HealthProbe
{
    public const string Argument = "--healthcheck";

    private const string UrlEnvironmentVariable = "APP_IMAGE_HEALTHCHECK_URL";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The port the container publishes, probed over loopback from inside it.
    /// </summary>
    private static readonly Uri DefaultUrl = new UriBuilder(
        Uri.UriSchemeHttp,
        "127.0.0.1",
        8080,
        "/health/ready").Uri;

    public static bool IsProbeRequested(string[] args) =>
        args.Length > 0 && args.Contains(Argument, StringComparer.Ordinal);

    public static async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (!TryResolveUrl(out Uri? url, out string? failure))
        {
            await Console.Error.WriteLineAsync(failure);
            return 2;
        }

        using var client = new HttpClient { Timeout = Timeout };

        try
        {
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return 0;
            }

            await Console.Error.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"health check: {url} returned {(int)response.StatusCode}"));

            return 1;
        }
        catch (HttpRequestException exception)
        {
            await Console.Error.WriteLineAsync($"health check: {url} is not reachable ({exception.Message})");
            return 1;
        }
        catch (TaskCanceledException)
        {
            await Console.Error.WriteLineAsync($"health check: {url} timed out");
            return 1;
        }
    }

    private static bool TryResolveUrl([NotNullWhen(true)] out Uri? url, [NotNullWhen(false)] out string? failure)
    {
        string? configured = Environment.GetEnvironmentVariable(UrlEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(configured))
        {
            url = DefaultUrl;
            failure = null;
            return true;
        }

        if (Uri.TryCreate(configured, UriKind.Absolute, out url))
        {
            failure = null;
            return true;
        }

        failure = $"health check: {UrlEnvironmentVariable}='{configured}' is not an absolute URL";
        return false;
    }
}
