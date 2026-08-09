namespace Api.Extensions;

public static class EndpointResultExtensions
{
    public static RouteHandlerBuilder ProducesResult<TResponse>(
        this RouteHandlerBuilder builder,
        int success = StatusCodes.Status200OK) =>
        builder
            .Produces<TResponse>(success)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
