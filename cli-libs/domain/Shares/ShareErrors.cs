using SharedKernel;

namespace Share.Domain.Shares;

/// <summary>
/// Failures the CLI detects while assembling and uploading a share, as opposed to failures
/// the API reports (see <see cref="Api.ShareApiErrors"/>).
/// </summary>
public static class ShareErrors
{
    public static Error OwnerNotConfigured() => Error.Failure(
      "Share.OwnerNotConfigured",
      "No owner is configured for this share. Run `share config set --user-id <id>`, " +
      "or pass --user-id on this command.");

    public static Error DirectoryNotFound(string path) => Error.NotFound(
      "Share.DirectoryNotFound",
      $"There is no directory at '{path}'.");

    public static Error DirectoryEmpty(string path) => Error.Problem(
      "Share.DirectoryEmpty",
      $"'{path}' contains no files, so there is nothing to share.");

    public static Error DirectoryUnreadable(string path, string reason) => Error.Failure(
      "Share.DirectoryUnreadable",
      $"'{path}' could not be read: {reason}");

    /// <summary>
    /// The API's file manifest carries sizes as a 32-bit value, so a single file cannot
    /// exceed <see cref="int.MaxValue"/> bytes (~2 GiB).
    /// </summary>
    public static Error FileTooLarge(string relativePath, long size) => Error.Problem(
      "Share.FileTooLarge",
      $"'{relativePath}' is {size} bytes. A single file may be at most {int.MaxValue} bytes.");

    public static Error FileUnreadable(string relativePath, string reason) => Error.Failure(
      "Share.FileUnreadable",
      $"'{relativePath}' could not be read: {reason}");

    /// <summary>
    /// The API returned upload targets that do not cover every file that was declared —
    /// the two are matched by relative path, never by position.
    /// </summary>
    public static Error MissingUploadUrl(string relativePath) => Error.Failure(
      "Share.MissingUploadUrl",
      $"The Share service did not return an upload URL for '{relativePath}'.");

    public static Error UploadFailed(string relativePath, string reason) => Error.Failure(
      "Share.UploadFailed",
      $"Uploading '{relativePath}' failed: {reason}");

    public static Error UploadRejected(string relativePath, int statusCode) => Error.Failure(
      "Share.UploadRejected",
      $"Storage rejected the upload of '{relativePath}' with HTTP {statusCode}. " +
      "Presigned URLs are short-lived — creating the share again issues fresh ones.");

    public static Error UploadTimedOut(string relativePath) => Error.Failure(
      "Share.UploadTimedOut",
      $"Uploading '{relativePath}' timed out.");
}
