using Share.Application.Abstractions.FileSystem;
using Share.Domain.Shares;
using SharedKernel;

namespace Share.Infrastructure.FileSystem;

/// <summary>
/// Walks a share root with <see cref="Directory.EnumerateFiles(string, string, EnumerationOptions)"/>.
/// </summary>
internal sealed class FileScanner : IFileScanner
{
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = true,

        // Take hidden and system entries too: "share this folder" means what is on disk,
        // and on Unix that routinely includes dotfiles the user does care about.
        AttributesToSkip = FileAttributes.None,

        // A file we cannot read is a failure worth reporting, not one to quietly drop —
        // a share is supposed to be the whole directory.
        IgnoreInaccessible = false
    };

    public Result<ScannedDirectory> Scan(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return Result.Failure<ScannedDirectory>(
                ShareErrors.DirectoryNotFound(directoryPath ?? string.Empty));
        }

        string root;

        try
        {
            root = Path.GetFullPath(directoryPath);
        }
        catch (Exception exception)
            when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return Result.Failure<ScannedDirectory>(
                ShareErrors.DirectoryUnreadable(directoryPath, exception.Message));
        }

        if (!Directory.Exists(root))
        {
            return Result.Failure<ScannedDirectory>(ShareErrors.DirectoryNotFound(root));
        }

        var files = new List<LocalFile>();

        try
        {
            foreach (string path in Directory.EnumerateFiles(root, "*", Options))
            {
                files.Add(new LocalFile(
                    ToRelativePath(root, path),
                    path,
                    new FileInfo(path).Length,
                    ContentTypes.ForFile(path)));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failure<ScannedDirectory>(
                ShareErrors.DirectoryUnreadable(root, exception.Message));
        }

        if (files.Count == 0)
        {
            return Result.Failure<ScannedDirectory>(ShareErrors.DirectoryEmpty(root));
        }

        // Enumeration order is whatever the file system hands back. Sorting makes a run
        // reproducible, so two shares of the same directory upload in the same order.
        files.Sort(static (left, right) =>
            string.CompareOrdinal(left.RelativePath, right.RelativePath));

        return Result.Success(new ScannedDirectory(root, files));
    }

    /// <summary>
    /// Relative paths travel to the API and are stored there, so they are normalised to
    /// forward slashes — a share created on Windows must read the same everywhere else.
    /// </summary>
    private static string ToRelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
}
