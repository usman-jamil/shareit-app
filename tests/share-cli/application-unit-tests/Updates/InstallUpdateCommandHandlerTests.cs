using NSubstitute;
using Share.Application.Abstractions.Updates;
using Share.Application.Updates.Install;
using Share.Domain.Updates;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Updates;

public sealed class InstallUpdateCommandHandlerTests
{
    private static InstallUpdateCommand Command(string version = "1.2.0", int callerProcessId = 1234) =>
        new(UpdateData.Version(version), UpdateData.ExecutablePath, callerProcessId);

    [Fact]
    public async Task Handle_Should_WaitStageAndReplace_AndThenDiscardTheDownload()
    {
        IUpdateProcessLauncher launcher = UpdateSubstitutes.Launcher();
        IUpdatePackageInstaller installer = UpdateSubstitutes.Installer();

        var handler = new InstallUpdateCommandHandler(
            UpdateSubstitutes.Catalog(),
            installer,
            launcher);

        Result<InstallUpdateResponse> result = await handler.Handle(
            Command(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ShouldBe(UpdateData.Version("1.2.0"));
        result.Value.TargetExecutablePath.ShouldBe(UpdateData.ExecutablePath);

        await launcher
            .Received(1)
            .WaitForExitAsync(1234, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());

        await installer
            .Received(1)
            .ReplaceAsync(
                UpdateData.Staged(),
                UpdateData.ExecutablePath,
                Arg.Any<CancellationToken>());

        installer.Received(1).Discard(UpdateData.Staged());
    }

    [Fact]
    public async Task Handle_Should_TouchNothing_WhenTheCallerIsStillRunning()
    {
        IUpdatePackageInstaller installer = UpdateSubstitutes.Installer();

        var handler = new InstallUpdateCommandHandler(
            UpdateSubstitutes.Catalog(),
            installer,
            UpdateSubstitutes.Launcher().FailsWait(UpdateErrors.CallerStillRunning(1234)));

        Result<InstallUpdateResponse> result = await handler.Handle(
            Command(),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.CallerStillRunning");

        // Replacing a binary that is still in use is the one thing this command must never
        // do, so nothing downstream of the wait may have run.
        await installer
            .DidNotReceive()
            .StageAsync(Arg.Any<ReleaseInfo>(), Arg.Any<CancellationToken>());

        await installer
            .DidNotReceive()
            .ReplaceAsync(
                Arg.Any<StagedUpdate>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_NotStage_WhenTheReleaseCannotBeResolved()
    {
        IUpdatePackageInstaller installer = UpdateSubstitutes.Installer();

        var handler = new InstallUpdateCommandHandler(
            UpdateSubstitutes.Catalog()
                .FailsGet(UpdateErrors.CatalogRateLimited()),
            installer,
            UpdateSubstitutes.Launcher());

        Result<InstallUpdateResponse> result = await handler.Handle(
            Command(),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.CatalogRateLimited");

        await installer
            .DidNotReceive()
            .StageAsync(Arg.Any<ReleaseInfo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_NotReplace_WhenTheDownloadCannotBeVerified()
    {
        IUpdatePackageInstaller installer = UpdateSubstitutes.Installer()
            .FailsStage(UpdateErrors.ChecksumMismatch("share-1.2.0-linux-x64.tar.gz"));

        var handler = new InstallUpdateCommandHandler(
            UpdateSubstitutes.Catalog(),
            installer,
            UpdateSubstitutes.Launcher());

        Result<InstallUpdateResponse> result = await handler.Handle(
            Command(),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.ChecksumMismatch");

        await installer
            .DidNotReceive()
            .ReplaceAsync(
                Arg.Any<StagedUpdate>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_DiscardTheDownload_EvenWhenTheSwapFails()
    {
        Error notWritable = UpdateErrors.TargetNotWritable(
            UpdateData.ExecutablePath,
            "permission denied");

        IUpdatePackageInstaller installer = UpdateSubstitutes.Installer()
            .FailsReplace(notWritable);

        var handler = new InstallUpdateCommandHandler(
            UpdateSubstitutes.Catalog(),
            installer,
            UpdateSubstitutes.Launcher());

        Result<InstallUpdateResponse> result = await handler.Handle(
            Command(),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(notWritable);

        // The download is spent either way; leaving it behind would fill the temp directory
        // one failed update at a time.
        installer.Received(1).Discard(UpdateData.Staged());
    }

    [Fact]
    public async Task Handle_Should_WaitForNothing_WhenThereIsNoCallerProcess()
    {
        IUpdateProcessLauncher launcher = UpdateSubstitutes.Launcher();

        var handler = new InstallUpdateCommandHandler(
            UpdateSubstitutes.Catalog(),
            UpdateSubstitutes.Installer(),
            launcher);

        Result<InstallUpdateResponse> result = await handler.Handle(
            Command(callerProcessId: 0),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        await launcher
            .Received(1)
            .WaitForExitAsync(0, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }
}
