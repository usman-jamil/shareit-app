using SharedKernel;

namespace Domain.ApiKeys;

public static class ApiKeyErrors
{
    public static Error NotFound(Guid apiKeyId) => Error.NotFound(
      "ApiKeys.NotFound",
      $"The Api Key with the Id = '{apiKeyId}' was not found");

    public static Error AlreadyRevoked(Guid apiKeyId) => Error.Failure(
      "ApiKeys.AlreadyRevoked",
      $"The Api Key with the Id = '{apiKeyId}' is already revoked.");

    public static Error InValid(Guid apiKeyId) => Error.Failure(
      "ApiKeys.InValid",
      $"The Api Key with the Id = '{apiKeyId}' is not valid.");
}

