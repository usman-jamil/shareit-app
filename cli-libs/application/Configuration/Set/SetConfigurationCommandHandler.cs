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
        Result<ShareApiSettings> current = await store.ReadAsync(cancellationToken);

        if (current.IsFailure)
        {
            return Result.Failure<ConfigurationResponse>(current.Error);
        }

        var updated = new ShareApiSettings(
            command.BaseUrl ?? current.Value.BaseUrl,
            command.TimeoutSeconds ?? current.Value.TimeoutSeconds);

        Result saved = await store.SaveAsync(updated, cancellationToken);

        return saved.IsFailure
            ? Result.Failure<ConfigurationResponse>(saved.Error)
            : Result.Success(ConfigurationResponse.From(store.Location, exists: true, updated));
    }
}
