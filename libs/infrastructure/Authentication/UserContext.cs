using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Infrastructure.Authentication;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor, IApplicationDbContext context) : IUserContext
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public Guid UserId =>
      context
        .ApiKeys
        .FirstOrDefault(apiKey => apiKey.Id == ApiKey)?.UserId ??
        throw new UserContextUnavailableException();

    public Guid ApiKey =>
      httpContextAccessor
        .HttpContext?
        .Request
        .Headers
        .TryGetValue(ApiKeyHeaderName, out StringValues apiKey) == true
          ? Guid.Parse(apiKey.ToString() ?? "")
          : throw new ApiKeyUnavailableException();
}
