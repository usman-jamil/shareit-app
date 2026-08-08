using System.Globalization;
using SharedKernel;

namespace Share.Domain.Updates;

/// <summary>
/// Failures of <c>share update</c>: reaching the release catalogue, choosing a release,
/// fetching and verifying its archive, and putting the new binary in place.
/// </summary>
public static class UpdateErrors
{
    // Talking to GitHub.

    public static Error CatalogUnreachable(string reason) => Error.Failure(
      "Update.CatalogUnreachable",
      $"Could not reach GitHub to look up releases: {reason}");

    public static Error CatalogTimeout() => Error.Failure(
      "Update.CatalogTimeout",
      "GitHub did not respond in time while looking up releases.");

    public static Error CatalogRateLimited() => Error.Failure(
      "Update.CatalogRateLimited",
      "GitHub is rate-limiting this machine. Wait a few minutes and try again.");

    public static Error CatalogUnexpected(int statusCode) => Error.Failure(
      "Update.CatalogUnexpected",
      $"GitHub returned HTTP {statusCode.ToString(CultureInfo.InvariantCulture)} " +
      "while looking up releases.");

    public static Error CatalogInvalidResponse() => Error.Failure(
      "Update.CatalogInvalidResponse",
      "GitHub returned a release listing that could not be understood.");

    public static Error RepositoryNotFound(string repository) => Error.NotFound(
      "Update.RepositoryNotFound",
      $"There is no repository at '{repository}', so releases cannot be listed.");

    // Choosing a release.

    public static Error NoReleasesPublished() => Error.NotFound(
      "Update.NoReleasesPublished",
      "No release of the CLI has been published yet.");

    public static Error ReleaseNotFound(SemanticVersion version) => Error.NotFound(
      "Update.ReleaseNotFound",
      $"There is no published release {version}.");

    public static Error InvalidVersion(string text) => Error.Problem(
      "Update.InvalidVersion",
      $"'{text}' is not a version. Use MAJOR.MINOR.PATCH, e.g. 1.3.2, " +
      "optionally with a suffix such as 1.3.2-beta.1.");

    // What this process is and where it lives.

    public static Error CurrentVersionUnknown() => Error.Failure(
      "Update.CurrentVersionUnknown",
      "This build does not report a version it can be compared against, so it cannot " +
      "decide whether an update is needed.");

    public static Error ExecutablePathUnknown() => Error.Failure(
      "Update.ExecutablePathUnknown",
      "The path of the running executable could not be determined, so there is nothing " +
      "to replace.");

    public static Error UnsupportedPlatform(string platform) => Error.Problem(
      "Update.UnsupportedPlatform",
      $"No release is published for {platform}, so this build cannot update itself.");

    public static Error NotSelfUpdatable(string path) => Error.Problem(
      "Update.NotSelfUpdatable",
      $"'{path}' was not installed from a release archive, so `share update` would not " +
      "produce a working binary. Update it the way it was installed.");

    // Fetching and verifying the archive.

    public static Error AssetNotFound(string assetName) => Error.NotFound(
      "Update.AssetNotFound",
      $"The release does not publish '{assetName}'.");

    public static Error ChecksumsUnavailable() => Error.Failure(
      "Update.ChecksumsUnavailable",
      $"The release does not publish {ReleasePackaging.ChecksumsAssetName}, so the " +
      "download cannot be verified.");

    public static Error ChecksumMissing(string assetName) => Error.Failure(
      "Update.ChecksumMissing",
      $"{ReleasePackaging.ChecksumsAssetName} does not list '{assetName}', so the " +
      "download cannot be verified.");

    /// <summary>
    /// The bytes that arrived are not the bytes the release published. Reported rather
    /// than retried: a mismatch is either corruption or interference, and neither is
    /// something to install.
    /// </summary>
    public static Error ChecksumMismatch(string assetName) => Error.Failure(
      "Update.ChecksumMismatch",
      $"'{assetName}' does not match the SHA-256 the release published. Nothing was " +
      "installed.");

    public static Error DownloadFailed(string assetName, string reason) => Error.Failure(
      "Update.DownloadFailed",
      $"Downloading '{assetName}' failed: {reason}");

    public static Error DownloadRejected(string assetName, int statusCode) => Error.Failure(
      "Update.DownloadRejected",
      $"Downloading '{assetName}' returned HTTP {statusCode.ToString(CultureInfo.InvariantCulture)}.");

    public static Error DownloadTimedOut(string assetName) => Error.Failure(
      "Update.DownloadTimedOut",
      $"Downloading '{assetName}' timed out.");

    public static Error ArchiveUnreadable(string assetName, string reason) => Error.Failure(
      "Update.ArchiveUnreadable",
      $"'{assetName}' could not be unpacked: {reason}");

    public static Error ExecutableMissing(string assetName, string executableName) =>
        Error.Failure(
          "Update.ExecutableMissing",
          $"'{assetName}' does not contain '{executableName}'.");

    // Swapping the binary.

    public static Error StagingFailed(string reason) => Error.Failure(
      "Update.StagingFailed",
      $"A working directory for the update could not be prepared: {reason}");

    public static Error LaunchFailed(string reason) => Error.Failure(
      "Update.LaunchFailed",
      $"The updater could not be started: {reason}");

    /// <summary>
    /// The updater waits for the process that spawned it to exit before touching the
    /// binary; giving up is safer than replacing a file that is still in use.
    /// </summary>
    public static Error CallerStillRunning(int processId) => Error.Failure(
      "Update.CallerStillRunning",
      $"Process {processId.ToString(CultureInfo.InvariantCulture)} is still running, so " +
      "its binary was left alone. Nothing was installed.");

    public static Error TargetNotWritable(string path, string reason) => Error.Failure(
      "Update.TargetNotWritable",
      $"'{path}' could not be replaced: {reason}. If it is installed system-wide, " +
      "re-run the update with the privileges that installed it.");
}
