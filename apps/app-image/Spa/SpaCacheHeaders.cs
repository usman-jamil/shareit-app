using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace AppImage.Host.Spa;

/// <summary>
/// Cache policy for the two kinds of thing this host serves from disk.
/// </summary>
/// <remarks>
/// Fingerprinted assets are immutable by construction — a content change produces a new file name —
/// so they get a year. The SPA document is the index of those names, so it must never be reused
/// from cache without revalidation or a deploy would leave browsers pointing at deleted bundles.
/// </remarks>
internal static class SpaCacheHeaders
{
    private const string ImmutableCacheControl = "public,max-age=31536000,immutable";
    private const string DocumentCacheControl = "no-cache,no-store,must-revalidate";

    public static void Apply(StaticFileResponseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsFingerprintedAsset(context.Context.Request.Path))
        {
            ApplyAsset(context.Context.Response);
        }
        else
        {
            ApplyDocument(context.Context.Response);
        }
    }

    public static void ApplyDocument(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Headers[HeaderNames.CacheControl] = DocumentCacheControl;
        response.Headers[HeaderNames.Pragma] = "no-cache";
        response.Headers.Remove(HeaderNames.Expires);
    }

    private static void ApplyAsset(HttpResponse response) =>
        response.Headers[HeaderNames.CacheControl] = ImmutableCacheControl;

    /// <summary>
    /// Only files under <see cref="WebAssets.AssetsDirectoryName"/> carry a content hash in their
    /// name. Anything else copied into the web root — <c>favicon.ico</c>, <c>robots.txt</c>,
    /// anything from <c>public/</c> — keeps a stable name across deploys and must revalidate.
    /// </summary>
    private static bool IsFingerprintedAsset(PathString path) =>
        path.StartsWithSegments("/" + WebAssets.AssetsDirectoryName, StringComparison.OrdinalIgnoreCase);
}
