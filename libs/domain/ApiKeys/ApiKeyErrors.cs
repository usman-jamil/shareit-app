using SharedKernel;

namespace Domain.ApiKeys;

public static class ApiKeyErrors
{
    public static Error NotFound(string apiKey) => Error.NotFound(
      "ApiKeys.NotFound",
      $"The Api Key with the Id = '{apiKey}' was not found");

    public static Error AlreadyRevoked(string apiKey) => Error.Failure(
      "ApiKeys.AlreadyRevoked",
      $"The Api Key with the Id = '{apiKey}' is already revoked.");

    public static Error InValid(string apiKey) => Error.Failure(
      "ApiKeys.InValid",
      $"The Api Key with the Id = '{apiKey}' is not valid.");
}

