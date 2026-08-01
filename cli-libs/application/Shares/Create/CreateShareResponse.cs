namespace Share.Application.Shares.Create;

/// <summary>
/// A finalized share: every file has been uploaded and the API has confirmed it.
/// </summary>
/// <param name="ShareId">Identifier the share can be read back by.</param>
/// <param name="Root">The absolute directory the files were taken from.</param>
/// <param name="FileCount">How many files were uploaded.</param>
/// <param name="TotalBytes">Their combined size.</param>
public sealed record CreateShareResponse(Guid ShareId, string Root, int FileCount, long TotalBytes);
