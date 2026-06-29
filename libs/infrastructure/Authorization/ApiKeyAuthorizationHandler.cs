using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.ApiKeys.Get;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Infrastructure.Authorization;

public class ApiKeyAuthorizationHandler(IUserContext userContext, IServiceProvider serviceProvider) : AuthorizationHandler<ApiKeyRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ApiKeyRequirement requirement)
    {
        string apiKey = userContext.ApiKey;
        using IServiceScope scope = serviceProvider.CreateScope();
        IQueryHandler<GetApiKeyQuery, ApiKeyResponse> handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<GetApiKeyQuery, ApiKeyResponse>>();
        var query = new GetApiKeyQuery(apiKey);
        Result<ApiKeyResponse> response = await handler.Handle(query, userContext.CancellationToken);

        if (response.IsSuccess)
        {
            context.Succeed(requirement);
        }
    }
}
