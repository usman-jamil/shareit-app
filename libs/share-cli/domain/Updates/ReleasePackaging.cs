namespace Share.Domain.Updates;

/// <summary>
/// How a release is packaged. Mirrors the <c>Package</c> step of
/// <c>.github/workflows/release-share-cli.yml</c> — if the archive naming changes there,
/// it changes here, and nowhere else.
/// </summary>
public static class ReleasePackaging
{
    /// <summary>
    /// The one asset holding <c>sha256sum</c> lines for every archive in the release.
    /// </summary>
    public const string ChecksumsAssetName = "SHA256SUMS.txt";

    private const string WindowsRuntimePrefix = "win-";
    private const string ExecutableStem = "share";

    /// <summary>
    /// The archive published for <paramref name="runtimeIdentifier"/>, e.g.
    /// <c>share-1.2.3-osx-arm64.tar.gz</c>.
    /// </summary>
    public static string ArchiveName(SemanticVersion version, string runtimeIdentifier) =>
        $"{ExecutableStem}-{version}-{runtimeIdentifier}.{ArchiveExtension(runtimeIdentifier)}";

    /// <summary>
    /// Windows archives are zips so they can be opened without extra tooling; every other
    /// target is a gzipped tar, which is what preserves the executable bit.
    /// </summary>
    public static string ArchiveExtension(string runtimeIdentifier) =>
        IsWindows(runtimeIdentifier) ? "zip" : "tar.gz";

    /// <summary>
    /// The name the executable has inside the archive.
    /// </summary>
    public static string ExecutableName(string runtimeIdentifier) =>
        IsWindows(runtimeIdentifier) ? $"{ExecutableStem}.exe" : ExecutableStem;

    public static bool IsWindows(string runtimeIdentifier) =>
        runtimeIdentifier is not null &&
        runtimeIdentifier.StartsWith(WindowsRuntimePrefix, StringComparison.Ordinal);
}
