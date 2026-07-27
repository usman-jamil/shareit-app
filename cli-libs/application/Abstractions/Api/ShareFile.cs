namespace Share.Application.Abstractions.Api;

/// <summary>
/// One stored file within a share.
/// </summary>
/// <param name="Sha256">
/// Content hash. Only meaningful once the share is finalized — while it is <c>pending</c>
/// the API holds a placeholder, since it has not seen the bytes yet.
/// </param>
public sealed record ShareFile(
    Guid Id,
    Guid ShareId,
    string RelativePath,
    string Sha256,
    string ContentType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Size);
