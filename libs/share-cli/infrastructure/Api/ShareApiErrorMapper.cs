using System.Net;
using System.Text.Json;
using Refit;
using Share.Domain.Api;
using SharedKernel;

namespace Share.Infrastructure.Api;

/// <summary>
/// Translates everything the transport can hand back — a ProblemDetails body, a bare
/// status code, a dead connection — into a <see cref="Error"/> the Application layer can
/// act on. This is the whole reason the Application layer never sees Refit.
/// </summary>
internal static class ShareApiErrorMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Maps a thrown transport exception. Refit splits these in two: <see cref="ApiException"/>
    /// means the API answered with a failure status, <see cref="ApiRequestException"/> means
    /// the request never produced a response at all.
    /// </summary>
    public static Error FromException(Exception exception) =>
        exception switch
        {
            ApiException apiException => FromApiException(apiException),
            ApiRequestException requestException => FromRequestException(requestException),
            HttpRequestException httpRequestException =>
                ShareApiErrors.Unreachable(httpRequestException.Message),
            _ => ShareApiErrors.Unexpected(exception.Message)
        };

    private static Error FromRequestException(ApiRequestException exception) =>
        exception.InnerException switch
        {
            // Refit wraps the HttpClient's own timeout as a cancellation.
            TaskCanceledException or TimeoutException => ShareApiErrors.Timeout(),
            HttpRequestException httpRequestException =>
                ShareApiErrors.Unreachable(httpRequestException.Message),
            _ => ShareApiErrors.Unreachable(exception.Message)
        };

    private static Error FromApiException(ApiException exception)
    {
        // 401/403 are produced by the authorization policy, not by a handler, so they
        // carry no ProblemDetails body worth reading.
        if (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return ShareApiErrors.Unauthorized();
        }

        ProblemDetailsResponse? problem = TryReadProblemDetails(exception.Content);

        if (problem is null || string.IsNullOrWhiteSpace(problem.Title))
        {
            return ShareApiErrors.Unexpected(
                $"HTTP {(int)exception.StatusCode} {exception.StatusCode}");
        }

        string code = problem.Title;
        string description = problem.Detail ?? string.Empty;

        return exception.StatusCode switch
        {
            HttpStatusCode.BadRequest => ToValidationError(problem, code, description),
            HttpStatusCode.NotFound => Error.NotFound(code, description),
            HttpStatusCode.Conflict => Error.Conflict(code, description),
            _ => Error.Failure(code, description)
        };
    }

    private static Error ToValidationError(
        ProblemDetailsResponse problem,
        string code,
        string description)
    {
        // A validation failure carries the individual rule violations in the `errors`
        // extension; preserve them so the CLI can list every problem at once.
        if (problem.Errors is not { Length: > 0 })
        {
            return new Error(code, description, ErrorType.Validation);
        }

        Error[] errors = problem.Errors
            .Select(error => new Error(
                error.Code ?? code,
                error.Description ?? string.Empty,
                ToErrorType(error.Type)))
            .ToArray();

        return new ValidationError(errors);
    }

    private static ProblemDetailsResponse? TryReadProblemDetails(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProblemDetailsResponse>(content, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ErrorType ToErrorType(int type) =>
        Enum.IsDefined(typeof(ErrorType), type) ? (ErrorType)type : ErrorType.Failure;
}
