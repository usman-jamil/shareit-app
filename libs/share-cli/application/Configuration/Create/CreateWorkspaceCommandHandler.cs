using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.Messaging;
using SharedKernel;

namespace Share.Application.Configuration.Create;

internal sealed class CreateWorkspaceCommandHandler(IConfigurationStore store)
    : ICommandHandler<CreateWorkspaceCommand, ConfigurationResponse>
{
    public async Task<Result<ConfigurationResponse>> Handle(
        CreateWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        Result created = await store.CreateWorkspaceAsync(command.Name, cancellationToken);

        if (created.IsFailure)
        {
            return Result.Failure<ConfigurationResponse>(created.Error);
        }

        // Read back rather than assume: the new workspace sets nothing, so this reports the
        // defaults the user now has to fill in.
        Result<ActiveWorkspace> workspace = await store.ReadAsync(cancellationToken);

        return workspace.IsFailure
            ? Result.Failure<ConfigurationResponse>(workspace.Error)
            : Result.Success(
                ConfigurationResponse.From(store.Location, store.Exists, workspace.Value));
    }
}
