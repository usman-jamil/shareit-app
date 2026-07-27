using SharedKernel;

namespace Share.Application.Abstractions.Api;

/// <summary>
/// The CLI's view of the Share HTTP API. One method per operation the API exposes,
/// named for how the CLI uses it rather than for the underlying route.
/// </summary>
/// <remarks>
/// <para>
/// Uploading a share is a three-step conversation, because file bytes never pass
/// through the API — they go straight to object storage via presigned URLs:
/// </para>
/// <list type="number">
///   <item><description><see cref="CreateShareAsync"/> — declare the files; receive a share id and one presigned upload URL per file. The share is now <c>pending</c>.</description></item>
///   <item><description>PUT each file's bytes to its <see cref="FileUploadTarget.UploadUrl"/>. This is a plain HTTP upload to storage, <b>not</b> part of this interface.</description></item>
///   <item><description><see cref="FinalizeShareAsync"/> — confirm the uploads; the share becomes <c>finalized</c>.</description></item>
/// </list>
/// <para>
/// Every method returns <see cref="Result"/> rather than throwing: transport failures
/// (unreachable host, timeout, unexpected status) and API-reported failures (not found,
/// conflict, validation) both arrive as a failure <see cref="Error"/>. The only exception
/// that propagates is <see cref="OperationCanceledException"/> when the caller's
/// <see cref="CancellationToken"/> is cancelled.
/// </para>
/// </remarks>
public interface IShareApiClient
{
    /// <summary>
    /// Looks up the owner a share will be created for.
    /// Fails with a <see cref="ErrorType.NotFound"/> error when no such user exists.
    /// </summary>
    Task<Result<UserDetails>> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Step 1 of an upload: registers the share and its file manifest, and returns a
    /// presigned upload URL per file. Nothing is uploaded yet — the share stays
    /// <c>pending</c> until <see cref="FinalizeShareAsync"/> is called.
    /// </summary>
    Task<Result<CreatedShare>> CreateShareAsync(
        CreateShareRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Step 3 of an upload: confirms every file has been uploaded and flips the share to
    /// <c>finalized</c>. Fails with a <see cref="ErrorType.Conflict"/> error if the share
    /// was already finalized, so this is safe to surface as a user-facing message rather
    /// than a retry.
    /// </summary>
    Task<Result> FinalizeShareAsync(Guid shareId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a share back with its files — used to report status, expiry and totals.
    /// Fails with a <see cref="ErrorType.NotFound"/> error when the share does not exist
    /// or has expired.
    /// </summary>
    Task<Result<ShareDetails>> GetShareAsync(Guid shareId, CancellationToken cancellationToken = default);
}
