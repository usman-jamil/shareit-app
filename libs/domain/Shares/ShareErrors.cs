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

    public static Error AlreadyFinalized(Guid shareId) => Error.Conflict(
      "Shares.AlreadyFinalized",
      $"The Share with the Id = '{shareId}' has already been finalized.");
}
