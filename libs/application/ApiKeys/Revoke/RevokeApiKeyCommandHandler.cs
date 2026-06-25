using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.ApiKeys;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ApiKeys.Revoke;

internal sealed class RevokeApiKeyCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IApiKeyHasher hasher)
    : ICommandHandler<RevokeApiKeyCommand>
{
    public async Task<Result> Handle(RevokeApiKeyCommand command, CancellationToken cancellationToken)
    {
        string[] parts = command.ApiKey.Split('_', 3);
        if (parts.Length != 3 || parts[0] != hasher.KeyPrefix)
        {
            return Result.Failure(ApiKeyErrors.NotFound(command.ApiKey));
        }
        
        string keyId  = parts[1];

        ApiKey? row = await context.ApiKeys
            .SingleOrDefaultAsync(k => k.KeyId == keyId, cancellationToken);

        if (row is null)
        {
            return Result.Failure(ApiKeyErrors.NotFound(command.ApiKey));
        }

        if (row.RevokedAt != null)
        {
            return Result.Failure(ApiKeyErrors.AlreadyRevoked(command.ApiKey));
        }

        row.RevokedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
