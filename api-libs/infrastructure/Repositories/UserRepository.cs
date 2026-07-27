using Domain.Users;
using Infrastructure.Database;

namespace Infrastructure.Repositories;

internal sealed class UserRepository(ApplicationDbContext dbContext) : Repository<User>(dbContext), IUserRepository;
