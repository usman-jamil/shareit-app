using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.GetById;
using SharedKernel;

namespace Api.Endpoints.Users;

public class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/{userId}", async (
            Guid userId,
            IQueryHandler<GetUserByIdQuery, UserResponse> handler,
            CancellationToken cancellationToken) =>
            {
                var query = new GetUserByIdQuery(userId);

                Result<UserResponse> result = await handler.Handle(query, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .Produces<Result<UserResponse>>()
            .RequireApiKey()
            .WithTags(Tags.Users);
    }
}
