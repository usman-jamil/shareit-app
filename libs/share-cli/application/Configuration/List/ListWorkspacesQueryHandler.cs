using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.Messaging;
using SharedKernel;

namespace Share.Application.Configuration.List;

internal sealed class ListWorkspacesQueryHandler(IConfigurationStore store)
    : IQueryHandler<ListWorkspacesQuery, WorkspacesResponse>
{
    public async Task<Result<WorkspacesResponse>> Handle(
        ListWorkspacesQuery query,
        CancellationToken cancellationToken)
    {
        Result<WorkspaceList> workspaces = await store.ListWorkspacesAsync(cancellationToken);

        return workspaces.IsFailure
            ? Result.Failure<WorkspacesResponse>(workspaces.Error)
            : Result.Success(
                WorkspacesResponse.From(store.Location, store.Exists, workspaces.Value));
    }
}
