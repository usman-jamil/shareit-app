using System.Net.Mime;
using AppImage.Host.Configuration;
using Microsoft.Net.Http.Headers;

namespace AppImage.Host.Spa;

/// <summary>
/// The terminal endpoint for anything the static file middleware and the API proxy did not handle.
/// <para>
/// It returns the SPA document only for requests that look like client-side routes. A request for
/// a file that is not on disk — <c>/assets/missing.js</c>, <c>/favicon-does-not-exist.ico</c> —
/// gets a 404, because answering it with HTML turns a broken deploy into a silent one: the browser
/// receives <c>text/html</c> where it expected JavaScript and fails much further from the cause.
/// </para>
/// </summary>
internal sealed class SpaFallbackHandler(ValidatedAppImageOptions options, ILogger<SpaFallbackHandler> logger)
{
    public async Task HandleAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PathString path = context.Request.Path;

        // Belt and braces. The proxy routes below own this prefix and are mapped first, so an
        // unmatched /api request is answered by the API itself. If the proxy were ever
        // misconfigured, this keeps the failure honest instead of serving HTML to an API client.
        if (path.StartsWithSegments(options.ApiPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Request to {Path} reached the SPA fallback; the API proxy did not match it.",
                path.Value);

            await WriteApiNotFoundAsync(context);
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (LooksLikeFileRequest(path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var index = new FileInfo(options.Web.IndexPath);
        if (!index.Exists)
        {
            // Validated at startup, so this means the web root went away underneath a running
            // container. Nothing useful to say to the caller; the readiness probe reports it.
            logger.LogError("The SPA document {IndexPath} is missing.", options.Web.IndexPath);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        SpaCacheHeaders.ApplyDocument(context.Response);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = MediaTypeNames.Text.Html;
        context.Response.ContentLength = index.Length;

        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        await context.Response.SendFileAsync(options.Web.IndexPath, context.RequestAborted);
    }

    /// <summary>
    /// A client-side route addresses a resource; a static file request addresses a file. The last
    /// path segment is what tells them apart: <c>/customers/123</c> has no extension,
    /// <c>/assets/index.abc123.js</c> does.
    /// </summary>
    private static bool LooksLikeFileRequest(PathString path)
    {
        string value = path.Value ?? "/";
        int lastSlash = value.LastIndexOf('/');
        ReadOnlySpan<char> lastSegment = value.AsSpan(lastSlash + 1);

        return lastSegment.Contains('.');
    }

    /// <summary>
    /// ProblemDetails rather than the SPA document, matching what the API returns for an unknown
    /// route. Carries no internal detail.
    /// </summary>
    private static async Task WriteApiNotFoundAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers[HeaderNames.CacheControl] = "no-store";

        await context.Response.WriteAsync(
            """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404}""",
            context.RequestAborted);
    }
}
