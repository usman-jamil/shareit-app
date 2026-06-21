namespace Domain.Files;

public interface IFileRepository
{
    Task<Domain.Files.File?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
