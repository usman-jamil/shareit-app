using SharedKernel;

namespace Share.Domain.Api;

/// <summary>
/// Failures that come from talking to the Share API itself, as opposed to failures the
/// API reports about the request (those arrive already carrying their own code and
/// description, and are passed through unchanged).
/// </summary>
public static class ShareApiErrors
{
    public static Error Unreachable(string reason) => Error.Failure(
      "ShareApi.Unreachable",
      $"Could not reach the Share service: {reason}");

    public static Error Timeout() => Error.Failure(
      "ShareApi.Timeout",
      "The Share service did not respond in time.");

    public static Error Unauthorized() => Error.Failure(
      "ShareApi.Unauthorized",
      "The Share service rejected the API key. Check that it is configured and still valid.");

    public static Error Unexpected(string reason) => Error.Failure(
      "ShareApi.Unexpected",
      $"The Share service returned an unexpected response: {reason}");

    public static Error InvalidResponse() => Error.Failure(
      "ShareApi.InvalidResponse",
      "The Share service returned a response that could not be understood.");
}
