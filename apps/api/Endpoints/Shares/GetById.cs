using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shares.GetById;
using SharedKernel;

namespace Api.Endpoints.Shares;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("shares/{shareId}", async (
            Guid shareId,
            IQueryHandler<GetShareByIdQuery, ShareResponse> handler,
            CancellationToken cancellationToken) =>
            {
                var query = new GetShareByIdQuery(shareId);

                Result<ShareResponse> result = await handler.Handle(query, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .Produces<Result<ShareResponse>>()
            .RequireApiKey()
            .WithTags(Tags.Shares);
    }
}
