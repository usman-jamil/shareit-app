using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shares.Finalize;
using SharedKernel;

namespace Api.Endpoints.Shares;

internal sealed class Finalize : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("shares/{shareId}/finalize", async (
            Guid shareId,
            ICommandHandler<FinalizeShareCommand> handler,
            CancellationToken cancellationToken) =>
            {
                var command = new FinalizeShareCommand(shareId);

                Result result = await handler.Handle(command, cancellationToken);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .Produces(StatusCodes.Status204NoContent)
            .RequireApiKey()
            .WithTags(Tags.Shares);
    }
}
