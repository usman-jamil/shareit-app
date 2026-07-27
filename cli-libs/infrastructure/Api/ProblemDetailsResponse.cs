namespace Share.Infrastructure.Api;

/// <summary>
/// The RFC 7807 payload the API returns for every failed request. The API builds it from
/// a <c>SharedKernel.Error</c>, so <see cref="Title"/> carries the error code,
/// <see cref="Detail"/> the description, and <see cref="Errors"/> the individual
/// validation failures when there are any.
/// </summary>
internal sealed record ProblemDetailsResponse
{
    public string? Title { get; init; }

    public string? Detail { get; init; }

    public int? Status { get; init; }

    public ProblemDetailsError[]? Errors { get; init; }
}

/// <summary>
/// One entry of the <c>errors</c> extension on a validation problem.
/// </summary>
internal sealed record ProblemDetailsError
{
    public string? Code { get; init; }

    public string? Description { get; init; }

    public int Type { get; init; }
}
