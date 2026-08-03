using NSubstitute;
using Share.Application.Abstractions.Updates;
using Share.Application.Updates.Check;
using Share.Domain.Updates;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Updates;

public sealed class CheckForUpdateQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReportAnUpgrade_WhenTheLatestReleaseIsNewer()
    {
        var handler = new CheckForUpdateQueryHandler(
            UpdateSubstitutes.Environment(currentVersion: "1.0.0"),
            UpdateSubstitutes.Catalog(latestVersion: "1.2.0"));

        Result<UpdateCheckResponse> result = await handler.Handle(
            new CheckForUpdateQuery(RequestedVersion: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Action.ShouldBe(UpdateAction.Upgrade);
        result.Value.CurrentVersion.ShouldBe(UpdateData.Version("1.0.0"));
        result.Value.TargetVersion.ShouldBe(UpdateData.Version("1.2.0"));
        result.Value.TagName.ShouldBe("sharecli-1.2.0");
    }

    [Fact]
    public async Task Handle_Should_ReportUpToDate_WhenTheLatestReleaseIsTheOneRunning()
    {
        var handler = new CheckForUpdateQueryHandler(
            UpdateSubstitutes.Environment(currentVersion: "1.2.0"),
            UpdateSubstitutes.Catalog(latestVersion: "1.2.0"));

        Result<UpdateCheckResponse> result = await handler.Handle(
            new CheckForUpdateQuery(RequestedVersion: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Action.ShouldBe(UpdateAction.UpToDate);
    }

    [Fact]
    public async Task Handle_Should_ReportADowngrade_WhenTheRequestedReleaseIsOlder()
    {
        IReleaseCatalog catalog = UpdateSubstitutes.Catalog();

        var handler = new CheckForUpdateQueryHandler(
            UpdateSubstitutes.Environment(currentVersion: "1.2.0"),
            catalog);

        Result<UpdateCheckResponse> result = await handler.Handle(
            new CheckForUpdateQuery(UpdateData.Version("1.0.0")),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Action.ShouldBe(UpdateAction.Downgrade);
        result.Value.TargetVersion.ShouldBe(UpdateData.Version("1.0.0"));
    }

    [Fact]
    public async Task Handle_Should_AskForTheExactRelease_WhenAVersionIsRequested()
    {
        IReleaseCatalog catalog = UpdateSubstitutes.Catalog();

        var handler = new CheckForUpdateQueryHandler(UpdateSubstitutes.Environment(), catalog);

        await handler.Handle(
            new CheckForUpdateQuery(UpdateData.Version("1.3.2")),
            TestContext.Current.CancellationToken);

        // Asking for a version the user named must not fall back to "latest": that would
        // report a release they never asked about.
        await catalog
            .Received(1)
            .GetAsync(UpdateData.Version("1.3.2"), Arg.Any<CancellationToken>());

        await catalog.DidNotReceive().GetLatestAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTheRunningBuildHasNoVersion()
    {
        IReleaseCatalog catalog = UpdateSubstitutes.Catalog();

        var handler = new CheckForUpdateQueryHandler(
            UpdateSubstitutes.Environment(currentVersion: null),
            catalog);

        Result<UpdateCheckResponse> result = await handler.Handle(
            new CheckForUpdateQuery(RequestedVersion: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.CurrentVersionUnknown");

        // Nothing to compare against, so there was no point asking.
        await catalog.DidNotReceive().GetLatestAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_PassTheCatalogFailureThrough()
    {
        Error unreachable = UpdateErrors.CatalogUnreachable("connection refused");

        var handler = new CheckForUpdateQueryHandler(
            UpdateSubstitutes.Environment(),
            UpdateSubstitutes.Catalog().FailsGetLatest(unreachable));

        Result<UpdateCheckResponse> result = await handler.Handle(
            new CheckForUpdateQuery(RequestedVersion: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(unreachable);
    }
}
