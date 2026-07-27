namespace Share.Application.Abstractions.Api;

/// <summary>
/// The result of step 1 of an upload: the share exists and is <c>pending</c>, and the
/// CLI now holds one presigned upload target per requested file.
/// </summary>
/// <param name="ShareId">Identifier needed to finalize or read the share back.</param>
/// <param name="Files">
/// One entry per file in the request, in no guaranteed order — match them to local files
/// by <see cref="FileUploadTarget.RelativePath"/>, not by position.
/// </param>
public sealed record CreatedShare(Guid ShareId, IReadOnlyCollection<FileUploadTarget> Files);
