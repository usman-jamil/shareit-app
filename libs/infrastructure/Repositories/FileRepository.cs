using Domain.Files;
using Infrastructure.Database;

namespace Infrastructure.Repositories;

internal sealed class FileRepository(ApplicationDbContext dbContext) : Repository<Domain.Files.File>(dbContext), IFileRepository;
