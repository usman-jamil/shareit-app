using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Authentication;
using Domain.ApiKeys;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ApiKeys.GetById;

internal sealed class GetApiKeyByIdQueryHandler(IApplicationDbContext context, IUserContext userContext)
  : IQueryHandler<GetApiKeyByIdQuery, ApiKeyResponse>
{
  public async Task<Result<ApiKeyResponse>> Handle(GetApiKeyByIdQuery query, CancellationToken cancellationToken)
  {
    ApiKeyResponse? todo = await context.ApiKeys
      .Where(apiKey => apiKey.Id == query.ApiKeyId && apiKey.UserId == userContext.UserId)
      .Select(apiKey => new ApiKeyResponse
      {
        Id = apiKey.Id,
        UserId = apiKey.UserId,
        KeyHash = apiKey.KeyHash,
        Prefix = apiKey.Prefix,
        CreatedAt = apiKey.CreatedAt,
        LastUsedAt = apiKey.LastUsedAt,
        RevokedAt = apiKey.RevokedAt,
        Label = apiKey.Label
      })
      .SingleOrDefaultAsync(cancellationToken);

    if (todo is null)
    {
      return Result.Failure<ApiKeyResponse>(ApiKeyErrors.NotFound(query.ApiKeyId));
    }

    return todo;
  }
}
