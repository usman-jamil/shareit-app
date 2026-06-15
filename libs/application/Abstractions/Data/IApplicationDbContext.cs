using Domain.ApiKeys;
using Domain.Shares;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<Domain.Files.File> Files { get; }
    DbSet<Share> Shares { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
