namespace Domain.Shares;

public interface IShareRepository
{
    Task<Share?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
