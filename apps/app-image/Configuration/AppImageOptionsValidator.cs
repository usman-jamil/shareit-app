using System.Diagnostics.CodeAnalysis;
using AppImage.Host.Spa;

namespace AppImage.Host.Configuration;

/// <summary>
/// Startup validation for <see cref="AppImageOptions"/>. Every failure here is a deployment mistake
/// that would otherwise surface as a confusing 404 or 502 at request time, so the host refuses to
/// start instead.
/// </summary>
internal static class AppImageOptionsValidator
{
    /// <summary>
    /// Validates <paramref name="options"/> and, on success, produces the derived values the rest of
    /// the host is wired from.
    /// </summary>
    public static bool TryValidate(
        AppImageOptions options,
        [NotNullWhen(true)] out ValidatedAppImageOptions? validated,
        out IReadOnlyList<string> failures)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        validated = null;

        string webRoot = string.IsNullOrWhiteSpace(options.WebRoot)
            ? string.Empty
            : Path.GetFullPath(options.WebRoot);

        if (webRoot.Length == 0)
        {
            errors.Add($"{AppImageOptions.SectionName}:WebRoot is required (APP_IMAGE_WEB_ROOT).");
        }
        else if (!Directory.Exists(webRoot))
        {
            errors.Add($"The web root '{webRoot}' does not exist. Set APP_IMAGE_WEB_ROOT to the directory holding the built React application.");
        }
        else if (!File.Exists(Path.Combine(webRoot, WebAssets.IndexFileName)))
        {
            errors.Add($"The web root '{webRoot}' does not contain {WebAssets.IndexFileName}. It must be the output of the web production build.");
        }

        if (!Uri.TryCreate(options.Api.Destination, UriKind.Absolute, out Uri? destination) ||
            !destination.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.Ordinal) &&
            !destination.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            errors.Add($"The API destination '{options.Api.Destination}' is not an absolute http(s) URL (APP_IMAGE_API_DESTINATION).");
            destination = null;
        }

        string prefix = (options.Api.PathPrefix ?? string.Empty).TrimEnd('/');
        if (prefix.Length < 2 || prefix[0] != '/')
        {
            errors.Add($"The API path prefix '{options.Api.PathPrefix}' must be an absolute path such as '/api' (APP_IMAGE_API_PATH_PREFIX).");
        }

        string healthPath = options.Api.HealthPath ?? string.Empty;
        if (healthPath.Length == 0 || healthPath[0] != '/')
        {
            errors.Add($"The API health path '{options.Api.HealthPath}' must be an absolute path such as '/health' (APP_IMAGE_API_HEALTH_PATH).");
        }

        if (options.Api.HealthTimeoutSeconds is < 1 or > 120)
        {
            errors.Add("The API health timeout must be between 1 and 120 seconds (APP_IMAGE_API_HEALTH_TIMEOUT_SECONDS).");
        }

        failures = errors;
        if (errors.Count > 0 || destination is null)
        {
            return false;
        }

        validated = new ValidatedAppImageOptions(
            new WebAssets(webRoot),
            destination,
            prefix,
            options.Api.StripPathPrefix,
            healthPath,
            TimeSpan.FromSeconds(options.Api.HealthTimeoutSeconds),
            options.ForwardedHeaders.TrustAllProxies);

        return true;
    }
}

/// <summary>
/// The validated, parsed form of <see cref="AppImageOptions"/>. Constructed once at startup; every
/// component reads from this rather than re-parsing strings per request.
/// </summary>
/// <param name="Web">The React build output this host serves.</param>
/// <param name="ApiDestination">Absolute base address of the internal API.</param>
/// <param name="ApiPathPrefix">Public path prefix owned by the API, without a trailing slash.</param>
/// <param name="StripApiPathPrefix">Whether the prefix is removed before forwarding.</param>
/// <param name="ApiHealthPath">Absolute path of the API's own health endpoint.</param>
/// <param name="ApiHealthTimeout">Readiness probe timeout.</param>
/// <param name="TrustAllProxies">Whether forwarded headers are accepted from any proxy.</param>
internal sealed record ValidatedAppImageOptions(
    WebAssets Web,
    Uri ApiDestination,
    string ApiPathPrefix,
    bool StripApiPathPrefix,
    string ApiHealthPath,
    TimeSpan ApiHealthTimeout,
    bool TrustAllProxies);
