namespace Share.Application.Abstractions.FileSystem;

/// <summary>
/// The outcome of walking a share root.
/// </summary>
/// <param name="Root">
/// The directory as the file system resolved it — absolute, with any <c>.</c> or <c>..</c>
/// segments removed. Reported back to the user so it is obvious what was shared.
/// </param>
/// <param name="Files">
/// Every file beneath <paramref name="Root"/>, ordered by relative path, never empty.
/// </param>
public sealed record ScannedDirectory(string Root, IReadOnlyList<LocalFile> Files);
