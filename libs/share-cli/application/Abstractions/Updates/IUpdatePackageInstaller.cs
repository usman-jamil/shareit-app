using SharedKernel;

namespace Share.Application.Abstractions.Updates;

/// <summary>
/// Turns a release into a binary on disk, and then into <em>the</em> binary.
/// </summary>
/// <remarks>
/// Download, checksum verification and unpacking are one step rather than three: they are
/// only ever done together, and a half-done one has nothing worth handing back. Putting
/// the result in place is separate because it is the only irreversible part, and the
/// updater does it after the process being replaced has exited.
/// </remarks>
public interface IUpdatePackageInstaller
{
    /// <summary>
    /// Downloads the archive this machine needs, verifies it against the SHA-256 the
    /// release published, and unpacks it into a temporary directory.
    /// </summary>
    Task<Result<StagedUpdate>> StageAsync(
        ReleaseInfo release,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces <paramref name="targetExecutablePath"/> with the staged binary, keeping the
    /// permissions the old file had. Fails rather than throwing when the path cannot be
    /// written — the usual cause is a system-wide install owned by another user.
    /// </summary>
    Task<Result> ReplaceAsync(
        StagedUpdate staged,
        string targetExecutablePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes what <see cref="StageAsync"/> created. Best effort: leaving a directory in
    /// the temp folder is not a reason to report a successful update as failed.
    /// </summary>
    void Discard(StagedUpdate staged);
}
