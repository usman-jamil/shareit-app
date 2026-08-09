using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.Messaging;
using SharedKernel;

namespace Share.Application.Configuration.Set;

internal sealed class SetConfigurationCommandHandler(IConfigurationStore store)
    : ICommandHandler<SetConfigurationCommand, ConfigurationResponse>
{
    public async Task<Result<ConfigurationResponse>> Handle(
        SetConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        // The write lands in whichever workspace is active — `config set` never names one,
        // so switching servers is `config activate` and nothing else.
        Result<ActiveWorkspace> current = await store.ReadAsync(cancellationToken);

        if (current.IsFailure)
        {
            return Result.Failure<ConfigurationResponse>(current.Error);
        }

        ShareApiSettings existing = current.Value.Settings;

        var updated = new ShareApiSettings(
            command.BaseUrl ?? existing.BaseUrl,
            command.TimeoutSeconds ?? existing.TimeoutSeconds,
            command.ApiKey ?? existing.ApiKey,
            command.UserId ?? existing.UserId);

        Result saved = await store.SaveAsync(updated, cancellationToken);

        return saved.IsFailure
            ? Result.Failure<ConfigurationResponse>(saved.Error)
            : Result.Success(ConfigurationResponse.From(
                store.Location,
                exists: true,
                new ActiveWorkspace(current.Value.Name, updated)));
    }
}
