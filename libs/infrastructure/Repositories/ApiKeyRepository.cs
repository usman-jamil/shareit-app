using Domain.ApiKeys;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class ApiKeyRepository(ApplicationDbContext dbContext)
    : Repository<ApiKey>(dbContext), IApiKeyRepository
{
    public async Task<ApiKey?> GetByKeyIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<ApiKey>()
            .FirstOrDefaultAsync(apiKey => apiKey.KeyId == id, cancellationToken);
    }
}
