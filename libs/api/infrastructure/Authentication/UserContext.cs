using System.Security.Claims;
using Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Infrastructure.Authentication;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public Guid UserId
    {
        get
        {
            string? value = httpContextAccessor.HttpContext?
                .User.FindFirst(ClaimTypes.NameIdentifier)
                ?.Value;

            return Guid.TryParse(value, out Guid id)
                ? id
                : throw new UserContextUnavailableException();
        }
    }

    public CancellationToken CancellationToken =>
        httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

    public string ApiKey =>
        httpContextAccessor.HttpContext?.Request.Headers
            .TryGetValue(ApiKeyHeaderName, out StringValues apiKey) == true
        && !StringValues.IsNullOrEmpty(apiKey)
            ? apiKey.ToString()
            : throw new ApiKeyUnavailableException();
}
