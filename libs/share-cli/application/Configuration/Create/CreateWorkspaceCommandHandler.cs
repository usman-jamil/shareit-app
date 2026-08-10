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

        if (command.Settings is { } settings)
        {
            // The new workspace is already the active one, so this lands in it. If the second
            // write fails the workspace stays behind, empty: it is named and selected, and
            // `config set` finishes the job — which is better than removing a workspace the
            // user has just been told about.
            Result saved = await store.SaveAsync(settings, cancellationToken);

            if (saved.IsFailure)
            {
                return Result.Failure<ConfigurationResponse>(saved.Error);
            }
        }

        // Read back rather than assume: this reports what the workspace now holds, including
        // the defaults the user still has to fill in.
        Result<ActiveWorkspace> workspace = await store.ReadAsync(cancellationToken);

        return workspace.IsFailure
            ? Result.Failure<ConfigurationResponse>(workspace.Error)
            : Result.Success(
                ConfigurationResponse.From(store.Location, store.Exists, workspace.Value));
    }
}
