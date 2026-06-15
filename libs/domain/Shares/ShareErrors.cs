using SharedKernel;

namespace Domain.Shares;

public static class ShareErrors
{
  public static Error NotFound(Guid shareId) => Error.NotFound(
    "Shares.NotFound",
    $"The Share with the Id = '{shareId}' was not found");

  public static Error Unauthorized() => Error.Failure(
    "Shares.Unauthorized",
    "You are not authorized to perform this action.");
}
