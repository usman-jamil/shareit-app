using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Share.Application.Abstractions.Updates;
using Share.Domain.Updates;
using Share.Infrastructure.Options;
using SharedKernel;

namespace Share.Infrastructure.Updates;

/// <summary>
/// Reads the CLI's releases from GitHub's REST API.
/// </summary>
/// <remarks>
/// <para>
/// The whole listing is fetched and filtered here rather than using
/// <c>/releases/latest</c>: that endpoint answers for the repository, not for a tag prefix,
/// so it would happily return a release of something else published from the same repo.
/// One request also serves both questions this interface asks.
/// </para>
/// <para>
/// Unauthenticated — the repository is public. GitHub rate-limits anonymous callers per IP,
/// which is reported as its own error rather than as a generic failure, because waiting is
/// the fix.
/// </para>
/// </remarks>
internal sealed class GitHubReleaseCatalog(HttpClient httpClient, IOptions<UpdateOptions> options)
    : IReleaseCatalog
{
    private const int PageSize = 100;

    public async Task<Result<ReleaseInfo>> GetLatestAsync(
        CancellationToken cancellationToken = default)
    {
        Result<List<ReleaseInfo>> releases = await ListAsync(cancellationToken);

        if (releases.IsFailure)
        {
            return Result.Failure<ReleaseInfo>(releases.Error);
        }

        // Stable only. Moving onto a prerelease is something the user asks for by name,
        // never something an update lands them on by accident.
        ReleaseInfo? latest = releases.Value
            .Where(release => !release.IsPreRelease)
            .OrderByDescending(release => release.Version)
            .FirstOrDefault();

        return latest is null
            ? Result.Failure<ReleaseInfo>(UpdateErrors.NoReleasesPublished())
            : Result.Success(latest);
    }

    public async Task<Result<ReleaseInfo>> GetAsync(
        SemanticVersion version,
        CancellationToken cancellationToken = default)
    {
        Result<List<ReleaseInfo>> releases = await ListAsync(cancellationToken);

        if (releases.IsFailure)
        {
            return Result.Failure<ReleaseInfo>(releases.Error);
        }

        // Matched on the parsed version rather than on the tag text, so `sharecli-1.3.2`
        // and `sharecli-v1.3.2` are both found by asking for 1.3.2.
        ReleaseInfo? match = releases.Value
            .FirstOrDefault(release => release.Version == version);

        return match is null
            ? Result.Failure<ReleaseInfo>(UpdateErrors.ReleaseNotFound(version))
            : Result.Success(match);
    }

    private async Task<Result<List<ReleaseInfo>>> ListAsync(
        CancellationToken cancellationToken)
    {
        UpdateOptions settings = options.Value;

        var path = new Uri(
            string.Create(
                CultureInfo.InvariantCulture,
                $"repos/{settings.RepositoryOwner}/{settings.RepositoryName}/releases?per_page={PageSize}"),
            UriKind.Relative);

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(path, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<List<ReleaseInfo>>(
                    ErrorFor(response.StatusCode, settings));
            }

            IReadOnlyList<GitHubRelease>? payload = await response.Content
                .ReadFromJsonAsync<IReadOnlyList<GitHubRelease>>(cancellationToken);

            return payload is null
                ? Result.Failure<List<ReleaseInfo>>(UpdateErrors.CatalogInvalidResponse())
                : Result.Success(Translate(payload, settings.TagPrefix));
        }
        catch (JsonException)
        {
            return Result.Failure<List<ReleaseInfo>>(UpdateErrors.CatalogInvalidResponse());
        }
        catch (HttpRequestException exception)
        {
            return Result.Failure<List<ReleaseInfo>>(
                UpdateErrors.CatalogUnreachable(exception.Message));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The caller's token is still live, so this was the client's own timeout rather
            // than the user cancelling.
            return Result.Failure<List<ReleaseInfo>>(UpdateErrors.CatalogTimeout());
        }
    }

    private static Error ErrorFor(HttpStatusCode statusCode, UpdateOptions settings) =>
        statusCode switch
        {
            HttpStatusCode.NotFound => UpdateErrors.RepositoryNotFound(
                $"{settings.RepositoryOwner}/{settings.RepositoryName}"),

            // GitHub answers an exhausted anonymous quota with 403, and a secondary rate
            // limit with 429. Neither is something the user can fix by changing the command.
            HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests =>
                UpdateErrors.CatalogRateLimited(),

            _ => UpdateErrors.CatalogUnexpected((int)statusCode)
        };

    /// <summary>
    /// Keeps the releases that are this CLI's: published, tagged with the prefix, and
    /// carrying a version that parses. Anything else is not addressable by
    /// <c>share update --version</c>, so it is dropped rather than reported.
    /// </summary>
    private static List<ReleaseInfo> Translate(
        IReadOnlyList<GitHubRelease> releases,
        string tagPrefix)
    {
        var translated = new List<ReleaseInfo>(releases.Count);

        foreach (GitHubRelease release in releases)
        {
            if (release.Draft || release.TagName is not { } tag)
            {
                continue;
            }

            if (!tag.StartsWith(tagPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!SemanticVersion.TryParse(tag[tagPrefix.Length..], out SemanticVersion? version))
            {
                continue;
            }

            translated.Add(new ReleaseInfo(
                version!,
                tag,
                release.PreRelease,
                release.PublishedAt,
                ToUri(release.HtmlUrl),
                TranslateAssets(release.Assets)));
        }

        return translated;
    }

    private static List<ReleaseAsset> TranslateAssets(
        IReadOnlyList<GitHubReleaseAsset>? assets)
    {
        if (assets is null)
        {
            return [];
        }

        var translated = new List<ReleaseAsset>(assets.Count);

        foreach (GitHubReleaseAsset asset in assets)
        {
            if (asset.Name is { Length: > 0 } name && ToUri(asset.BrowserDownloadUrl) is { } url)
            {
                translated.Add(new ReleaseAsset(name, url, asset.Size));
            }
        }

        return translated;
    }

    private static Uri? ToUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null;
}
