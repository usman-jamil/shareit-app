using Share.Domain.Shares;

namespace Share.Application.Abstractions.Api;

/// <summary>
/// A share as the API reports it, with its files.
/// </summary>
/// <param name="Status">
/// One of the <see cref="ShareStatus"/> values — compare against those constants rather
/// than raw strings.
/// </param>
/// <param name="TotalBytes">Sum of all file sizes; wider than a single file's size.</param>
public sealed record ShareDetails(
    Guid Id,
    Guid OwnerUserId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ExpiresAt,
    int ConfiguredTtlMinutes,
    long TotalBytes,
    int FileCount,
    IReadOnlyCollection<ShareFile> Files)
{
    /// <summary>
    /// <see langword="true"/> once every file has been uploaded and confirmed.
    /// </summary>
    public bool IsFinalized => Status == ShareStatus.Finalized;
}
