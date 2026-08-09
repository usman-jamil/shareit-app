using NSubstitute;
using Share.Application.Abstractions.Configuration;
using Share.Application.Configuration;
using Share.Application.Configuration.Activate;
using Share.Application.Configuration.Create;
using Share.Domain.Configuration;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Configuration;

/// <summary>
/// Both handlers do the same two things — change which workspace is active, then report
/// what the CLI is now pointed at — so they are covered together.
/// </summary>
public class WorkspaceCommandHandlerTests
{
    private const string Location = "/home/test/.shareit/config.yaml";

    private readonly IConfigurationStore _store = Substitute.For<IConfigurationStore>();
    private readonly CreateWorkspaceCommandHandler _create;
    private readonly ActivateWorkspaceCommandHandler _activate;

    public WorkspaceCommandHandlerTests()
    {
        _store.Location.Returns(Location);
        _store.Exists.Returns(true);
        _store.CreateWorkspaceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _store.ActivateWorkspaceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _store.ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ActiveWorkspace("development", ShareApiSettings.Empty)));

        _create = new CreateWorkspaceCommandHandler(_store);
        _activate = new ActivateWorkspaceCommandHandler(_store);
    }

    [Fact]
    public async Task Create_Should_AddTheWorkspace_AndReportItAsActiveWithEverythingDefaulted()
    {
        Result<ConfigurationResponse> result = await _create.Handle(
            new CreateWorkspaceCommand("development"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Workspace.ShouldBe("development");
        result.Value.BaseUrl.ShouldBe(ShareApiDefaults.BaseUrl);
        result.Value.BaseUrlIsDefault.ShouldBeTrue();
        result.Value.ApiKeyIsSet.ShouldBeFalse();
        await _store.Received(1).CreateWorkspaceAsync("development", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_Should_NotReport_WhenTheWorkspaceAlreadyExists()
    {
        Error error = ConfigurationErrors.WorkspaceAlreadyExists(Location, "development");
        _store.CreateWorkspaceAsync("development", Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        Result<ConfigurationResponse> result = await _create.Handle(
            new CreateWorkspaceCommand("development"),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        await _store.DidNotReceive().ReadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activate_Should_SwitchTheWorkspace_AndReportWhatItNowPointsAt()
    {
        Result<ConfigurationResponse> result = await _activate.Handle(
            new ActivateWorkspaceCommand("development"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Workspace.ShouldBe("development");
        await _store.Received(1).ActivateWorkspaceAsync("development", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activate_Should_NotReport_WhenThereIsNoSuchWorkspace()
    {
        Error error = ConfigurationErrors.WorkspaceNotFound(Location, "staging");
        _store.ActivateWorkspaceAsync("staging", Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        Result<ConfigurationResponse> result = await _activate.Handle(
            new ActivateWorkspaceCommand("staging"),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        await _store.DidNotReceive().ReadAsync(Arg.Any<CancellationToken>());
    }
}
