using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.Messaging;
using SharedKernel;

namespace Share.Application.Configuration.Get;

internal sealed class GetConfigurationQueryHandler(IConfigurationStore store)
    : IQueryHandler<GetConfigurationQuery, ConfigurationResponse>
{
    public async Task<Result<ConfigurationResponse>> Handle(
        GetConfigurationQuery query,
        CancellationToken cancellationToken)
    {
        Result<ActiveWorkspace> workspace = await store.ReadAsync(cancellationToken);

        return workspace.IsFailure
            ? Result.Failure<ConfigurationResponse>(workspace.Error)
            : Result.Success(
                ConfigurationResponse.From(store.Location, store.Exists, workspace.Value));
    }
}
