using Domain.ApiKeys;
using Infrastructure.Database;

namespace Infrastructure.Repositories;

internal sealed class ApiKeyRepository(ApplicationDbContext dbContext) : Repository<ApiKey>(dbContext), IApiKeyRepository;
