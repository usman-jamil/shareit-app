namespace Share.Application.Abstractions.Api;

/// <summary>
/// A single file the CLI intends to upload.
/// </summary>
/// <param name="RelativePath">
/// Path relative to the share root, forward-slash separated (e.g. <c>docs/images/logo.png</c>).
/// </param>
/// <param name="Size">Size in bytes, known from the local file before uploading.</param>
/// <param name="ContentType">
/// MIME type to store the file as. <see langword="null"/> when it cannot be inferred
/// from the extension.
/// </param>
public sealed record FileUploadRequest(string RelativePath, int Size, string? ContentType);
