namespace Application.Files.Create;

/// <summary>
/// A single file the client intends to upload as part of a share. The path is
/// relative to the share root and may contain nested segments (e.g.
/// <c>docs/images/logo.png</c>) to support recursive directory uploads.
/// </summary>
public sealed record FileUpload(string RelativePath, int Size, string? ContentType);
