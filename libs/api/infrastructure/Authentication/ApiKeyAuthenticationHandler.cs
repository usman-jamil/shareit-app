using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.ApiKeys.Get;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Authentication;

public sealed class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUserContext userContext,
    IQueryHandler<GetApiKeyQuery, ApiKeyResponse> handler)
    : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>(options, logger, encoder, null)
{
    public const string SchemeName = "ApiKey";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string apiKey;
        try
        {
            apiKey = userContext.ApiKey;
        }
        catch (ApiKeyUnavailableException)
        {
            return AuthenticateResult.NoResult(); // no header → challenge issues 401
        }

        Result<ApiKeyResponse> response =
            await handler.Handle(new GetApiKeyQuery(apiKey), Context.RequestAborted);

        if (response.IsFailure)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, response.Value.UserId.ToString()),
        ];

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
