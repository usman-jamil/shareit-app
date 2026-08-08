using Share.Application.Abstractions.Messaging;
using Share.Application.Abstractions.Updates;
using Share.Domain.Updates;
using SharedKernel;

namespace Share.Application.Updates.Check;

/// <summary>
/// Looks a release up and compares it with the running build. Reads only — nothing here
/// downloads, writes or launches anything, so <c>--check</c> is safe to run anywhere.
/// </summary>
internal sealed class CheckForUpdateQueryHandler(
    IApplicationEnvironment environment,
    IReleaseCatalog catalog)
    : IQueryHandler<CheckForUpdateQuery, UpdateCheckResponse>
{
    public async Task<Result<UpdateCheckResponse>> Handle(
        CheckForUpdateQuery query,
        CancellationToken cancellationToken)
    {
        if (environment.CurrentVersion is not { } current)
        {
            return Result.Failure<UpdateCheckResponse>(UpdateErrors.CurrentVersionUnknown());
        }

        // An explicit version is looked up by name so that asking for one that does not
        // exist says so, rather than silently reporting the latest instead.
        Result<ReleaseInfo> release = query.RequestedVersion is { } requested
            ? await catalog.GetAsync(requested, cancellationToken)
            : await catalog.GetLatestAsync(cancellationToken);

        if (release.IsFailure)
        {
            return Result.Failure<UpdateCheckResponse>(release.Error);
        }

        ReleaseInfo target = release.Value;

        return Result.Success(new UpdateCheckResponse(
            current,
            target.Version,
            target.TagName,
            target.IsPreRelease,
            target.PublishedAt,
            target.ReleaseUrl,
            ActionFor(current, target.Version)));
    }

    private static UpdateAction ActionFor(SemanticVersion current, SemanticVersion target) =>
        target.CompareTo(current) switch
        {
            > 0 => UpdateAction.Upgrade,
            < 0 => UpdateAction.Downgrade,
            _ => UpdateAction.UpToDate
        };
}
