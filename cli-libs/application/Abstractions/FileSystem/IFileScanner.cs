using SharedKernel;

namespace Share.Application.Abstractions.FileSystem;

/// <summary>
/// Reads the local directory a share is being made from.
/// </summary>
public interface IFileScanner
{
    /// <summary>
    /// Walks <paramref name="directoryPath"/> recursively and returns every file beneath
    /// it, ordered by relative path. Nothing is filtered out — hidden files and dotted
    /// directories included — so what is shared is exactly what is on disk.
    /// </summary>
    /// <returns>
    /// A failure when the directory is missing, unreadable, or holds no files at all;
    /// otherwise a <see cref="ScannedDirectory"/> with at least one file.
    /// </returns>
    Result<ScannedDirectory> Scan(string directoryPath);
}
