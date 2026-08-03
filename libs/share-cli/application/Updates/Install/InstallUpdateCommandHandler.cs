using Share.Application.Abstractions.Messaging;
using Share.Application.Abstractions.Updates;
using SharedKernel;

namespace Share.Application.Updates.Install;

/// <summary>
/// Sequences the update itself: wait, fetch, verify, swap. The only place the order of
/// those four is decided.
/// </summary>
/// <remarks>
/// Nothing on disk is touched until the archive has been downloaded and matched against
/// the SHA-256 the release published, so a failure at any point up to the swap leaves the
/// installed binary exactly as it was. The swap replaces one file and is the only step
/// that cannot be undone.
/// </remarks>
internal sealed class InstallUpdateCommandHandler(
    IReleaseCatalog catalog,
    IUpdatePackageInstaller installer,
    IUpdateProcessLauncher launcher)
    : ICommandHandler<InstallUpdateCommand, InstallUpdateResponse>
{
    /// <summary>
    /// How long to give the process being replaced to exit. It was on its way out before
    /// this one started, so anything approaching this is a process that is not going to
    /// leave — waiting longer would not change that.
    /// </summary>
    private static readonly TimeSpan CallerExitTimeout = TimeSpan.FromSeconds(30);

    public async Task<Result<InstallUpdateResponse>> Handle(
        InstallUpdateCommand command,
        CancellationToken cancellationToken)
    {
        Result waited = await launcher.WaitForExitAsync(
            command.CallerProcessId,
            CallerExitTimeout,
            cancellationToken);

        if (waited.IsFailure)
        {
            return Result.Failure<InstallUpdateResponse>(waited.Error);
        }

        Result<ReleaseInfo> release = await catalog.GetAsync(command.Version, cancellationToken);

        if (release.IsFailure)
        {
            return Result.Failure<InstallUpdateResponse>(release.Error);
        }

        Result<StagedUpdate> staged = await installer.StageAsync(release.Value, cancellationToken);

        if (staged.IsFailure)
        {
            return Result.Failure<InstallUpdateResponse>(staged.Error);
        }

        try
        {
            Result replaced = await installer.ReplaceAsync(
                staged.Value,
                command.TargetExecutablePath,
                cancellationToken);

            return replaced.IsFailure
                ? Result.Failure<InstallUpdateResponse>(replaced.Error)
                : Result.Success(new InstallUpdateResponse(
                    release.Value.Version,
                    release.Value.TagName,
                    command.TargetExecutablePath));
        }
        finally
        {
            // Whether the swap worked or not, the download is spent. Discarding it is best
            // effort by contract, so this cannot turn a successful update into a failure.
            installer.Discard(staged.Value);
        }
    }
}
