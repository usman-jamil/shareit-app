using System.Globalization;
using NSubstitute;
using Share.Application.Abstractions.Updates;
using Share.Application.Updates.Apply;
using Share.Application.Updates.Install;
using Share.Domain.Updates;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Updates;

public sealed class ApplyUpdateCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_StartTheUpdater_WithTheResolvedVersionTargetAndCaller()
    {
        IUpdateProcessLauncher launcher = UpdateSubstitutes.Launcher();

        var handler = new ApplyUpdateCommandHandler(
            UpdateSubstitutes.Environment(currentVersion: "1.0.0", processId: 1234),
            UpdateSubstitutes.Catalog(latestVersion: "1.2.0"),
            launcher);

        Result<ApplyUpdateResponse> result = await handler.Handle(
            new ApplyUpdateCommand(RequestedVersion: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TargetVersion.ShouldBe(UpdateData.Version("1.2.0"));
        result.Value.TargetExecutablePath.ShouldBe(UpdateData.ExecutablePath);
        result.Value.UpdaterProcessId.ShouldBe(UpdateSubstitutes.UpdaterProcessId);

        IReadOnlyList<string> arguments = launcher
            .ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IUpdateProcessLauncher.Start))
            .GetArguments()[0] as IReadOnlyList<string> ?? [];

        arguments.ShouldBe(
        [
            UpdaterCommandLine.Verb,
            UpdaterCommandLine.VersionOption, "1.2.0",
            UpdaterCommandLine.TargetOption, UpdateData.ExecutablePath,
            UpdaterCommandLine.CallerProcessIdOption, 1234.ToString(CultureInfo.InvariantCulture)
        ]);
    }

    [Fact]
    public async Task Handle_Should_PassTheRequestedVersionOn_RatherThanTheLatest()
    {
        IReleaseCatalog catalog = UpdateSubstitutes.Catalog(latestVersion: "1.2.0");

        var handler = new ApplyUpdateCommandHandler(
            UpdateSubstitutes.Environment(currentVersion: "1.2.0"),
            catalog,
            UpdateSubstitutes.Launcher());

        Result<ApplyUpdateResponse> result = await handler.Handle(
            new ApplyUpdateCommand(UpdateData.Version("1.0.0")),
            TestContext.Current.CancellationToken);

        // A downgrade is carried out as asked — the handler has no opinion about direction.
        result.IsSuccess.ShouldBeTrue();
        result.Value.TargetVersion.ShouldBe(UpdateData.Version("1.0.0"));

        await catalog.DidNotReceive().GetLatestAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Refuse_WhenTheBuildWasNotInstalledFromAReleaseArchive()
    {
        IUpdateProcessLauncher launcher = UpdateSubstitutes.Launcher();

        var handler = new ApplyUpdateCommandHandler(
            UpdateSubstitutes.Environment(isReleaseBuild: false),
            UpdateSubstitutes.Catalog(),
            launcher);

        Result<ApplyUpdateResponse> result = await handler.Handle(
            new ApplyUpdateCommand(RequestedVersion: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.NotSelfUpdatable");
        result.Error.Description.ShouldContain(UpdateData.ExecutablePath);

        launcher.DidNotReceive().Start(Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task Handle_Should_Refuse_WhenNoArchiveIsPublishedForThePlatform()
    {
        var handler = new ApplyUpdateCommandHandler(
            UpdateSubstitutes.Environment(runtimeIdentifier: null),
            UpdateSubstitutes.Catalog(),
            UpdateSubstitutes.Launcher());

        Result<ApplyUpdateResponse> result = await handler.Handle(
            new ApplyUpdateCommand(RequestedVersion: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.UnsupportedPlatform");
        result.Error.Description.ShouldContain("Test OS");
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTheExecutablePathIsUnknown()
    {
        var handler = new ApplyUpdateCommandHandler(
            UpdateSubstitutes.Environment(executablePath: null),
            UpdateSubstitutes.Catalog(),
            UpdateSubstitutes.Launcher());

        Result<ApplyUpdateResponse> result = await handler.Handle(
            new ApplyUpdateCommand(RequestedVersion: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.ExecutablePathUnknown");
    }

    [Fact]
    public async Task Handle_Should_NotStartTheUpdater_WhenTheReleaseCannotBeResolved()
    {
        IUpdateProcessLauncher launcher = UpdateSubstitutes.Launcher();

        var handler = new ApplyUpdateCommandHandler(
            UpdateSubstitutes.Environment(),
            UpdateSubstitutes.Catalog()
                .FailsGet(UpdateErrors.ReleaseNotFound(UpdateData.Version("9.9.9"))),
            launcher);

        Result<ApplyUpdateResponse> result = await handler.Handle(
            new ApplyUpdateCommand(UpdateData.Version("9.9.9")),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.ReleaseNotFound");

        launcher.DidNotReceive().Start(Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task Handle_Should_PassTheLauncherFailureThrough()
    {
        Error launchFailed = UpdateErrors.LaunchFailed("permission denied");

        var handler = new ApplyUpdateCommandHandler(
            UpdateSubstitutes.Environment(),
            UpdateSubstitutes.Catalog(),
            UpdateSubstitutes.Launcher().FailsStart(launchFailed));

        Result<ApplyUpdateResponse> result = await handler.Handle(
            new ApplyUpdateCommand(RequestedVersion: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(launchFailed);
    }
}
