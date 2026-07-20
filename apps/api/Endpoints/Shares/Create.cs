using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Files.Create;
using Application.Shares.Create;
using Infrastructure.Options;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Api.Endpoints.Shares;

internal sealed class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("shares", async (
            Request request,
            ICommandHandler<CreateShareCommand, CreateShareResponse> handler,
            IOptions<ShareOptions> shareOptions,
            CancellationToken cancellationToken) =>
            {
                var command = new CreateShareCommand(
                    request.OwnerUserId,
                    request.ConfiguredTtlMinutes ?? shareOptions.Value.ConfiguredTtlMinutes,
                    request.Files);

                Result<CreateShareResponse> result = await handler.Handle(command, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .Produces<Result<CreateShareResponse>>()
            .RequireApiKey()
            .WithTags(Tags.Shares);
    }

    private sealed record Request(
        Guid OwnerUserId,
        int? ConfiguredTtlMinutes,
        IReadOnlyCollection<FileUpload> Files);
}
