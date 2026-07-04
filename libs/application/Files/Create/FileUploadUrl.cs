namespace Application.Files.Create;

/// <summary>
/// The presigned URL the client should <c>PUT</c> the file contents to, paired
/// with the relative path it was requested for so the client can match them up.
/// </summary>
public sealed record FileUploadUrl(string RelativePath, Uri UploadUrl);
