using Share.Domain.Updates;

namespace Share.Application.Updates.Check;

/// <summary>
/// The installed version set against a published one.
/// </summary>
/// <param name="CurrentVersion">What is installed now.</param>
/// <param name="TargetVersion">The release that was looked up.</param>
/// <param name="TagName">Its tag, e.g. <c>sharecli-1.3.2</c>.</param>
/// <param name="IsPreRelease">Whether that release is marked as a prerelease.</param>
/// <param name="PublishedAt">When it was published, when that is known.</param>
/// <param name="ReleaseUrl">Its release page, for the notes.</param>
/// <param name="Action">
/// What installing it would amount to. <see cref="UpdateAction.UpToDate"/> means the
/// release is the version already running.
/// </param>
public sealed record UpdateCheckResponse(
    SemanticVersion CurrentVersion,
    SemanticVersion TargetVersion,
    string TagName,
    bool IsPreRelease,
    DateTimeOffset? PublishedAt,
    Uri? ReleaseUrl,
    UpdateAction Action);
