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
    IUserContext userContext)
    : ICommandHandler<RevokeApiKeyCommand>
{
    public async Task<Result> Handle(RevokeApiKeyCommand command, CancellationToken cancellationToken)
    {
        ApiKey? apiKey = await context.ApiKeys
            .SingleOrDefaultAsync(t => t.Id == command.ApiKeyId && t.UserId == userContext.UserId, cancellationToken);

        if (apiKey is null)
        {
            return Result.Failure(ApiKeyErrors.NotFound(command.ApiKeyId));
        }

        if (apiKey.RevokedAt != null)
        {
            return Result.Failure(ApiKeyErrors.AlreadyRevoked(command.ApiKeyId));
        }

        apiKey.RevokedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
