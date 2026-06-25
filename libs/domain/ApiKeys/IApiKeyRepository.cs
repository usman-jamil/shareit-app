namespace Domain.ApiKeys;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(ApiKey apiKey);
}
