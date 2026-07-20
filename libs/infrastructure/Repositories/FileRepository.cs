using Domain.Files;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class FileRepository(ApplicationDbContext dbContext)
    : Repository<Domain.Files.File>(dbContext), IFileRepository
{
    public async Task<List<Domain.Files.File>> GetByShareIdAsync(
        Guid shareId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<Domain.Files.File>()
            .Where(file => file.ShareId == shareId)
            .ToListAsync(cancellationToken);
    }
}
