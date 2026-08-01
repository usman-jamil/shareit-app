using Share.Application.Abstractions.FileSystem;
using SharedKernel;

namespace Share.Application.Abstractions.Storage;

/// <summary>
/// Step 2 of an upload: sends a file's bytes to the presigned URL the API issued.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="Api.IShareApiClient"/>. This talks to object
/// storage, not to the Share API — a different host, no API key, and no
/// <c>Result</c> envelope to unwrap. Widening the API client to cover it would send the
/// CLI's credentials to a third party.
/// </remarks>
public interface IFileUploader
{
    /// <summary>
    /// PUTs <paramref name="file"/> to <paramref name="uploadUrl"/>. Returns a failure
    /// rather than throwing for anything the user can act on: an unreadable file, a
    /// rejected or expired URL, a dead connection, a timeout.
    /// </summary>
    Task<Result> UploadAsync(
        Uri uploadUrl,
        LocalFile file,
        CancellationToken cancellationToken = default);
}
