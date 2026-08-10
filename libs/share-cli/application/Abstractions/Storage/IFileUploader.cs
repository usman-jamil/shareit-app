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
    /// <param name="uploadUrl">The presigned URL the API issued for this file.</param>
    /// <param name="file">The local file to send.</param>
    /// <param name="bytesUploaded">
    /// Told the running total of bytes handed to the transport for this file as it goes, or
    /// <see langword="null"/> when nothing is watching. It is a total and not an increment,
    /// so a body that has to be rewound can restate where it now is.
    /// <para>
    /// "Handed to the transport", not "acknowledged by storage": a file small enough to fit
    /// the socket's send buffer is counted before it has left the machine. Good enough to
    /// drive a progress bar, not evidence that anything arrived — only the returned
    /// <c>Result</c> is that.
    /// </para>
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result> UploadAsync(
        Uri uploadUrl,
        LocalFile file,
        IProgress<long>? bytesUploaded = null,
        CancellationToken cancellationToken = default);
}
