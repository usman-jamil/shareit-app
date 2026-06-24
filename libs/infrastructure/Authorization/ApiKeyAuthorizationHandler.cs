using Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Authorization;

public class ApiKeyAuthorizationHandler(IUserContext userContext, IServiceProvider serviceProvider) : AuthorizationHandler<ApiKeyRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ApiKeyRequirement requirement)
    {
        Guid apiKey = userContext.ApiKey;
        using IServiceScope scope = serviceProvider.CreateScope();
        AuthorizationService authorizationService = scope.ServiceProvider.GetRequiredService<AuthorizationService>();

        if (await authorizationService.IsApiKeyValid(apiKey))
        {
            context.Succeed(requirement);
        }
    }
}
