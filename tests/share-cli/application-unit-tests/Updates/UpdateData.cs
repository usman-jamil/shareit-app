using Share.Application.Abstractions.Updates;
using Share.Domain.Updates;

namespace Share.Application.UnitTests.Updates;

/// <summary>
/// Canned releases for the update handler tests. Assets are named the way
/// <see cref="ReleasePackaging"/> names them, so a test that cares about asset selection
/// is exercising the real convention.
/// </summary>
internal static class UpdateData
{
    public const string RuntimeIdentifier = "linux-x64";

    public const string ExecutablePath = "/usr/local/bin/share";

    public static SemanticVersion Version(string text) =>
        SemanticVersion.TryParse(text, out SemanticVersion? version)
            ? version!
            : throw new ArgumentException($"'{text}' is not a version.", nameof(text));

    public static ReleaseInfo Release(string version, bool isPreRelease = false) =>
        Release(Version(version), isPreRelease);

    public static ReleaseInfo Release(SemanticVersion version, bool isPreRelease = false) =>
        new(
            version,
            $"sharecli-{version}",
            isPreRelease,
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            new Uri($"https://github.com/owner/repo/releases/tag/sharecli-{version}"),
            Assets(version));

    public static StagedUpdate Staged() =>
        new("/tmp/share-cli-update/package/extracted/share", "/tmp/share-cli-update/package");

    private static ReleaseAsset[] Assets(SemanticVersion version)
    {
        string archive = ReleasePackaging.ArchiveName(version, RuntimeIdentifier);

        return
        [
            new ReleaseAsset(archive, new Uri($"https://downloads.example/{archive}"), 32_000_000),
            new ReleaseAsset(
                ReleasePackaging.ChecksumsAssetName,
                new Uri($"https://downloads.example/{ReleasePackaging.ChecksumsAssetName}"),
                562)
        ];
    }
}
