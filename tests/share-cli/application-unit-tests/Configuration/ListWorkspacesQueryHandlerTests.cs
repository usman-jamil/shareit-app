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

    private void Lists(string active, params WorkspaceSummary[] workspaces) =>
        _store
            .ListWorkspacesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new WorkspaceList(active, workspaces)));

    [Fact]
    public async Task Handle_Should_ReportTheWorkspaces_AndWhichIsActive()
    {
        Lists(
            "development",
            new WorkspaceSummary(ConfigurationWorkspaces.DefaultName, null),
            new WorkspaceSummary("development", "https://dev.example.com"),
            new WorkspaceSummary("production", "https://api.example.com"));

        Result<WorkspacesResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Location.ShouldBe(Location);
        result.Value.Active.ShouldBe("development");
        result.Value.Workspaces.Select(workspace => workspace.Name)
            .ShouldBe([ConfigurationWorkspaces.DefaultName, "development", "production"]);
        result.Value.Workspaces.Select(workspace => workspace.IsActive)
            .ShouldBe([false, true, false]);
        result.Value.ActiveIsMissing.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReportEachWorkspacesBaseUrl_AndFallBackToTheDefault()
    {
        Lists(
            "production",
            new WorkspaceSummary("production", "https://api.example.com"),
            new WorkspaceSummary("local", null));

        Result<WorkspacesResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Workspaces[0].BaseUrl.ShouldBe("https://api.example.com");
        result.Value.Workspaces[0].BaseUrlIsDefault.ShouldBeFalse();

        // A workspace that sets no URL still reaches a server — the one it would default to
        // — so the listing shows that rather than a blank cell.
        result.Value.Workspaces[1].BaseUrl.ShouldBe(ShareApiDefaults.BaseUrl.ToString());
        result.Value.Workspaces[1].BaseUrlIsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReportABaseUrlThatIsNotAUrl_AsItIsWritten()
    {
        // The store hands over what the file says without parsing it, and this listing is
        // how the user sees that it is wrong.
        Lists("production", new WorkspaceSummary("production", "api.example.com"));

        Result<WorkspacesResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Workspaces[0].BaseUrl.ShouldBe("api.example.com");
        result.Value.Workspaces[0].BaseUrlIsDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_MatchTheActiveWorkspaceIgnoringCase()
    {
        Lists("Development", new WorkspaceSummary("development", null));

        Result<WorkspacesResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ActiveIsMissing.ShouldBeFalse();
        result.Value.Workspaces[0].IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_FlagTheActiveWorkspace_WhenTheFileDoesNotDefineIt()
    {
        // A hand-edited file can point at a workspace that is not there. Listing has to
        // keep working — it is how the user finds out.
        Lists("staging", new WorkspaceSummary(ConfigurationWorkspaces.DefaultName, null));

        Result<WorkspacesResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ActiveIsMissing.ShouldBeTrue();
        result.Value.Workspaces.ShouldAllBe(workspace => !workspace.IsActive);
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
