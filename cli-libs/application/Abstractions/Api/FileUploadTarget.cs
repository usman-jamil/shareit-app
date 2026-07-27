namespace Share.Application.Abstractions.Api;

/// <summary>
/// Where to PUT one file's bytes. The URL is presigned and short-lived, so upload
/// promptly and do not persist it.
/// </summary>
/// <param name="RelativePath">Matches the path supplied in the create request.</param>
/// <param name="UploadUrl">Presigned storage URL accepting an HTTP PUT of the file bytes.</param>
public sealed record FileUploadTarget(string RelativePath, Uri UploadUrl);
