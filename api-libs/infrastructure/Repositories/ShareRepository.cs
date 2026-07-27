using Domain.Shares;
using Infrastructure.Database;

namespace Infrastructure.Repositories;

internal sealed class ShareRepository(ApplicationDbContext dbContext) : Repository<Share>(dbContext), IShareRepository;
