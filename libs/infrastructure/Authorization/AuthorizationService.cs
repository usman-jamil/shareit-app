using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authorization;

public class AuthorizationService(IApplicationDbContext context)
{
    public async Task<bool> IsApiKeyValid(Guid apiKey)
    {
        return await context.ApiKeys.AnyAsync(key => key.Id == apiKey && key.RevokedAt == null);
    }
}
