using System.Globalization;
using Share.Application.Abstractions.Messaging;
using Share.Application.Abstractions.Updates;
using Share.Application.Updates.Install;
using Share.Domain.Updates;
using SharedKernel;

namespace Share.Application.Updates.Apply;

/// <summary>
/// The first half of <c>share update</c>: check that this build can be replaced at all,
/// resolve the release, and start the process that will do it.
/// </summary>
/// <remarks>
/// Everything expensive — the download, the checksum, the swap — is deliberately left to
/// the second instance. This one only has to fail fast on the things that are cheap to
/// know and awkward to discover halfway through: an unpublished version, a platform with
/// no archive, a build that was never installed from one.
/// </remarks>
internal sealed class ApplyUpdateCommandHandler(
    IApplicationEnvironment environment,
    IReleaseCatalog catalog,
    IUpdateProcessLauncher launcher)
    : ICommandHandler<ApplyUpdateCommand, ApplyUpdateResponse>
{
    public async Task<Result<ApplyUpdateResponse>> Handle(
        ApplyUpdateCommand command,
        CancellationToken cancellationToken)
    {
        if (environment.CurrentVersion is not { } current)
        {
            return Result.Failure<ApplyUpdateResponse>(UpdateErrors.CurrentVersionUnknown());
        }

        if (environment.ExecutablePath is not { } executablePath)
        {
            return Result.Failure<ApplyUpdateResponse>(UpdateErrors.ExecutablePathUnknown());
        }

        // A build that keeps its assemblies beside the host is not something a one-file
        // swap can replace, and half-replacing it would leave nothing that runs.
        if (!environment.IsReleaseBuild)
        {
            return Result.Failure<ApplyUpdateResponse>(
                UpdateErrors.NotSelfUpdatable(executablePath));
        }

        if (environment.RuntimeIdentifier is null)
        {
            return Result.Failure<ApplyUpdateResponse>(
                UpdateErrors.UnsupportedPlatform(environment.PlatformDescription));
        }

        Result<ReleaseInfo> release = command.RequestedVersion is { } requested
            ? await catalog.GetAsync(requested, cancellationToken)
            : await catalog.GetLatestAsync(cancellationToken);

        if (release.IsFailure)
        {
            return Result.Failure<ApplyUpdateResponse>(release.Error);
        }

        ReleaseInfo target = release.Value;

        Result<UpdaterProcess> updater = launcher.Start(
            ArgumentsFor(target.Version, executablePath, environment.ProcessId));

        return updater.IsFailure
            ? Result.Failure<ApplyUpdateResponse>(updater.Error)
            : Result.Success(new ApplyUpdateResponse(
                current,
                target.Version,
                target.TagName,
                executablePath,
                updater.Value.ProcessId));
    }

    /// <summary>
    /// The resolved version is passed rather than what the user typed, so the updater
    /// installs exactly the release that was reported here — not whatever "latest" has
    /// become by the time it runs.
    /// </summary>
    private static string[] ArgumentsFor(
        SemanticVersion version,
        string executablePath,
        int callerProcessId) =>
        [
            UpdaterCommandLine.Verb,
            UpdaterCommandLine.VersionOption, version.ToString(),
            UpdaterCommandLine.TargetOption, executablePath,
            UpdaterCommandLine.CallerProcessIdOption,
                callerProcessId.ToString(CultureInfo.InvariantCulture)
        ];
}
