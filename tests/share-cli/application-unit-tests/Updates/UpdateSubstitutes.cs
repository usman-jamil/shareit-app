using NSubstitute;
using Share.Application.Abstractions.Updates;
using Share.Domain.Updates;
using SharedKernel;

namespace Share.Application.UnitTests.Updates;

/// <summary>
/// Substitutes for everything the update use cases reach outside themselves, each arranged
/// so that the whole update succeeds. A test re-arranges only the one call it is about —
/// usually to make it fail — so it reads as the one thing it is about rather than as four
/// lines of setup.
/// </summary>
internal static class UpdateSubstitutes
{
    public const int UpdaterProcessId = 4242;

    /// <summary>
    /// A released build on a platform that has an archive, at
    /// <paramref name="currentVersion"/>.
    /// </summary>
    public static IApplicationEnvironment Environment(
        string? currentVersion = "1.0.0",
        string? executablePath = UpdateData.ExecutablePath,
        string? runtimeIdentifier = UpdateData.RuntimeIdentifier,
        bool isReleaseBuild = true,
        int processId = 100)
    {
        IApplicationEnvironment environment = Substitute.For<IApplicationEnvironment>();

        environment.CurrentVersion.Returns(
            currentVersion is null ? null : UpdateData.Version(currentVersion));
        environment.ExecutablePath.Returns(executablePath);
        environment.RuntimeIdentifier.Returns(runtimeIdentifier);
        environment.IsReleaseBuild.Returns(isReleaseBuild);
        environment.ProcessId.Returns(processId);
        environment.PlatformDescription.Returns("Test OS (Unknown)");

        return environment;
    }

    /// <summary>
    /// A catalogue whose latest release is <paramref name="latestVersion"/> and which has a
    /// release for every version asked of it by name.
    /// </summary>
    public static IReleaseCatalog Catalog(string latestVersion = "1.2.0")
    {
        IReleaseCatalog catalog = Substitute.For<IReleaseCatalog>();

        catalog
            .GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(UpdateData.Release(latestVersion)));

        catalog
            .GetAsync(Arg.Any<SemanticVersion>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Success(
                UpdateData.Release(call.ArgAt<SemanticVersion>(0))));

        return catalog;
    }

    public static IUpdatePackageInstaller Installer()
    {
        IUpdatePackageInstaller installer = Substitute.For<IUpdatePackageInstaller>();

        installer
            .StageAsync(Arg.Any<ReleaseInfo>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(UpdateData.Staged()));

        installer
            .ReplaceAsync(
                Arg.Any<StagedUpdate>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return installer;
    }

    public static IUpdateProcessLauncher Launcher()
    {
        IUpdateProcessLauncher launcher = Substitute.For<IUpdateProcessLauncher>();

        launcher
            .Start(Arg.Any<IReadOnlyList<string>>())
            .Returns(Result.Success(
                new UpdaterProcess(UpdaterProcessId, "/tmp/share-cli-update/updater/share")));

        launcher
            .WaitForExitAsync(
                Arg.Any<int>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        return launcher;
    }

    public static IReleaseCatalog FailsGetLatest(this IReleaseCatalog catalog, Error error)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        catalog
            .GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ReleaseInfo>(error));

        return catalog;
    }

    public static IReleaseCatalog FailsGet(this IReleaseCatalog catalog, Error error)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        catalog
            .GetAsync(Arg.Any<SemanticVersion>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ReleaseInfo>(error));

        return catalog;
    }

    public static IUpdatePackageInstaller FailsStage(
        this IUpdatePackageInstaller installer,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(installer);

        installer
            .StageAsync(Arg.Any<ReleaseInfo>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<StagedUpdate>(error));

        return installer;
    }

    public static IUpdatePackageInstaller FailsReplace(
        this IUpdatePackageInstaller installer,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(installer);

        installer
            .ReplaceAsync(
                Arg.Any<StagedUpdate>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        return installer;
    }

    public static IUpdateProcessLauncher FailsStart(
        this IUpdateProcessLauncher launcher,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(launcher);

        launcher
            .Start(Arg.Any<IReadOnlyList<string>>())
            .Returns(Result.Failure<UpdaterProcess>(error));

        return launcher;
    }

    public static IUpdateProcessLauncher FailsWait(
        this IUpdateProcessLauncher launcher,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(launcher);

        launcher
            .WaitForExitAsync(
                Arg.Any<int>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        return launcher;
    }
}
