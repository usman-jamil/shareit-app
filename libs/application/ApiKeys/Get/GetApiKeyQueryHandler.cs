using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.ApiKeys;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ApiKeys.Get;

internal sealed class GetApiKeyQueryHandler(IApplicationDbContext context, IApiKeyHasher hasher)
  : IQueryHandler<GetApiKeyQuery, ApiKeyResponse>
{
    public async Task<Result<ApiKeyResponse>> Handle(GetApiKeyQuery query, CancellationToken cancellationToken)
    {
        string[] parts = query.ApiKey.Split('_', 3);
        if (parts.Length != 3 || parts[0] != hasher.KeyPrefix)
        {
            return Result.Failure<ApiKeyResponse>(ApiKeyErrors.NotFound(query.ApiKey));
        }

        string keyId = parts[1];
        string secret = parts[2];

        ApiKey? row = await context.ApiKeys
            .SingleOrDefaultAsync(k => k.KeyId == keyId, cancellationToken);

        if (row is null)
        {
            return Result.Failure<ApiKeyResponse>(ApiKeyErrors.NotFound(query.ApiKey));
        }

        if (row.RevokedAt is not null)
        {
            return Result.Failure<ApiKeyResponse>(ApiKeyErrors.InValid(query.ApiKey));
        }

        if (!hasher.Verify(secret, row.KeyHash))
        {
            return Result.Failure<ApiKeyResponse>(ApiKeyErrors.InValid(query.ApiKey));
        }

        return new ApiKeyResponse
        {
            Id = row.Id,
            UserId = row.UserId,
            KeyHash = row.KeyHash,
            Prefix = row.Prefix,
            CreatedAt = row.CreatedAt,
            LastUsedAt = row.LastUsedAt,
            RevokedAt = row.RevokedAt,
            Label = row.Label
        };
    }
}
