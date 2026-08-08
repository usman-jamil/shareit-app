namespace Share.Application.Abstractions.Updates;

/// <summary>
/// One file published with a release.
/// </summary>
/// <param name="Name">File name, e.g. <c>share-1.2.3-linux-x64.tar.gz</c>.</param>
/// <param name="DownloadUrl">Where the bytes are. Opaque — it is fetched, never parsed.</param>
/// <param name="Size">Size in bytes as the release reports it.</param>
public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size);
