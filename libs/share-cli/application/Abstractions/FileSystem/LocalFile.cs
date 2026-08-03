namespace Share.Application.Abstractions.FileSystem;

/// <summary>
/// One file found on disk beneath the share root.
/// </summary>
/// <param name="RelativePath">
/// Path relative to the share root, always forward-slash separated so a share created on
/// Windows reads the same as one created on macOS or Linux. This is the identity of the
/// file for the whole upload conversation — the API echoes it back on the upload targets.
/// </param>
/// <param name="FullPath">
/// Where the bytes actually live. Opaque to the Application layer: it is handed back to
/// <see cref="Storage.IFileUploader"/> and never interpreted.
/// </param>
/// <param name="Size">Size in bytes, as reported by the file system before uploading.</param>
/// <param name="ContentType">
/// MIME type inferred from the extension, or <see langword="null"/> when the extension is
/// not one we recognise.
/// </param>
public sealed record LocalFile(string RelativePath, string FullPath, long Size, string? ContentType);
