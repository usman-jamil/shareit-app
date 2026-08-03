using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.ApiKeys.Get;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SharedKernel;

namespace Infrastructure.Authentication;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor, IQueryHandler<GetApiKeyQuery, ApiKeyResponse> handler) : IUserContext
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public Guid UserId
    {
        get
        {
            var query = new GetApiKeyQuery(ApiKey);

            Result<ApiKeyResponse> response = handler.Handle(query, CancellationToken).Result;
            return response.IsFailure ? throw new UserContextUnavailableException() : response.Value.UserId;
        }
    }

    public CancellationToken CancellationToken => httpContextAccessor.HttpContext?.RequestAborted
                                                  ?? CancellationToken.None;

    public string ApiKey =>
      httpContextAccessor
        .HttpContext?
        .Request
        .Headers
        .TryGetValue(ApiKeyHeaderName, out StringValues apiKey) == true
          ? apiKey.ToString()
          : throw new ApiKeyUnavailableException();
}
