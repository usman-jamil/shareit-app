using NSubstitute;
using Share.Application.Abstractions.Configuration;
using Share.Application.Configuration.List;
using Share.Domain.Configuration;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Configuration;

public class ListWorkspacesQueryHandlerTests
{
    private const string Location = "/home/test/.shareit/config.yaml";

    private readonly IConfigurationStore _store = Substitute.For<IConfigurationStore>();
    private readonly ListWorkspacesQueryHandler _handler;

    public ListWorkspacesQueryHandlerTests()
    {
        _store.Location.Returns(Location);
        _store.Exists.Returns(true);

        _handler = new ListWorkspacesQueryHandler(_store);
    }

    private Task<Result<WorkspacesResponse>> Handle() =>
        _handler.Handle(new ListWorkspacesQuery(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_Should_ReportTheWorkspaces_AndWhichIsActive()
    {
        _store
            .ListWorkspacesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new WorkspaceList(
                "development",
                [ConfigurationWorkspaces.DefaultName, "development", "production"])));

        Result<WorkspacesResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Location.ShouldBe(Location);
        result.Value.Active.ShouldBe("development");
        result.Value.Names.ShouldBe([ConfigurationWorkspaces.DefaultName, "development", "production"]);
        result.Value.ActiveIsMissing.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_MatchTheActiveWorkspaceIgnoringCase()
    {
        _store
            .ListWorkspacesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new WorkspaceList("Development", ["development"])));

        Result<WorkspacesResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ActiveIsMissing.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_FlagTheActiveWorkspace_WhenTheFileDoesNotDefineIt()
    {
        // A hand-edited file can point at a workspace that is not there. Listing has to
        // keep working — it is how the user finds out.
        _store
            .ListWorkspacesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new WorkspaceList(
                "staging",
                [ConfigurationWorkspaces.DefaultName])));

        Result<WorkspacesResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ActiveIsMissing.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnTheStoreFailure_WhenTheFileCannotBeParsed()
    {
        Error error = ConfigurationErrors.Unparseable(Location, "mapping values are not allowed");
        _store
            .ListWorkspacesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<WorkspaceList>(error));

        Result<WorkspacesResponse> result = await Handle();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }
}
