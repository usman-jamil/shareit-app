using SharedKernel;

namespace Domain.Files;

public static class FileErrors
{
    public static Error NotFound(Guid fileId) => Error.NotFound(
      "Files.NotFound",
      $"The file with the Id = '{fileId}' was not found");
}

