using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.Messaging;
using SharedKernel;

namespace Share.Application.Configuration.Activate;

internal sealed class ActivateWorkspaceCommandHandler(IConfigurationStore store)
    : ICommandHandler<ActivateWorkspaceCommand, ConfigurationResponse>
{
    public async Task<Result<ConfigurationResponse>> Handle(
        ActivateWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        Result activated = await store.ActivateWorkspaceAsync(command.Name, cancellationToken);

        if (activated.IsFailure)
        {
            return Result.Failure<ConfigurationResponse>(activated.Error);
        }

        // Reporting what the CLI is now pointed at is the whole point of the command: the
        // user is switching servers and wants to see which one they landed on.
        Result<ActiveWorkspace> workspace = await store.ReadAsync(cancellationToken);

        return workspace.IsFailure
            ? Result.Failure<ConfigurationResponse>(workspace.Error)
            : Result.Success(
                ConfigurationResponse.From(store.Location, store.Exists, workspace.Value));
    }
}
