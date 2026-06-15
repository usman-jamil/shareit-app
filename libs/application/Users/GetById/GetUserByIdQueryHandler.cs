using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.GetById;

internal sealed class GetUserByIdQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetUserByIdQuery, UserResponse>
{
  public async Task<Result<UserResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
  {
    UserResponse? user = await context.Users
      .Where(user => user.Id == query.UserId)
      .Select(user => new UserResponse
      {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        CreatedAt = user.CreatedAt
      })
      .SingleOrDefaultAsync(cancellationToken);

    if (user is null)
    {
      return Result.Failure<UserResponse>(UserErrors.NotFound(query.UserId));
    }

    return user;
  }
}
