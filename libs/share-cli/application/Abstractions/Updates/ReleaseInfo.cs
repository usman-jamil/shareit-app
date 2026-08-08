using Share.Domain.Updates;

namespace Share.Application.Abstractions.Updates;

/// <summary>
/// A published release of the CLI, as the update use cases need it.
/// </summary>
/// <param name="Version">The version the tag encodes, with the tag prefix stripped.</param>
/// <param name="TagName">The tag as published, e.g. <c>sharecli-1.2.3</c>.</param>
/// <param name="IsPreRelease">Whether the release is marked as a prerelease.</param>
/// <param name="PublishedAt">When it was published, when the catalogue reports it.</param>
/// <param name="ReleaseUrl">The release page, for pointing the user at the notes.</param>
/// <param name="Assets">Everything published with it, including the checksums file.</param>
public sealed record ReleaseInfo(
    SemanticVersion Version,
    string TagName,
    bool IsPreRelease,
    DateTimeOffset? PublishedAt,
    Uri? ReleaseUrl,
    IReadOnlyList<ReleaseAsset> Assets);
