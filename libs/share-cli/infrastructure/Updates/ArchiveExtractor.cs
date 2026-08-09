using System.Formats.Tar;
using System.IO.Compression;
using Share.Domain.Updates;
using SharedKernel;

namespace Share.Infrastructure.Updates;

/// <summary>
/// Unpacks a release archive with the in-box readers — <c>ZipFile</c> for Windows targets,
/// <c>TarFile</c> over a <c>GZipStream</c> for the rest.
/// </summary>
/// <remarks>
/// Both refuse entries that would land outside the destination directory, so a tampered
/// archive cannot write over anything else on the way past the checksum. Extracting the tar
/// on Unix also restores the executable bit the workflow packed.
/// </remarks>
internal static class ArchiveExtractor
{
    public static async Task<Result> ExtractAsync(
        string archivePath,
        string destinationDirectory,
        string runtimeIdentifier,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(destinationDirectory);

            if (ReleasePackaging.IsWindows(runtimeIdentifier))
            {
                await ZipFile.ExtractToDirectoryAsync(
                    archivePath,
                    destinationDirectory,
                    overwriteFiles: true,
                    cancellationToken);

                return Result.Success();
            }

            await using FileStream archive = File.OpenRead(archivePath);
            await using var decompressed = new GZipStream(archive, CompressionMode.Decompress);

            await TarFile.ExtractToDirectoryAsync(
                decompressed,
                ResolvePhysicalPath(destinationDirectory),
                overwriteFiles: true,
                cancellationToken);

            return Result.Success();
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or NotSupportedException
                // A malformed archive should read as one, not as a crash mid-update: the
                // tar reader reports some bad input as an argument exception.
                or ArgumentException)
        {
            return Result.Failure(
                UpdateErrors.ArchiveUnreadable(Path.GetFileName(archivePath), exception.Message));
        }
    }

    /// <summary>
    /// Expands every symbolic link in <paramref name="path"/>, so what is handed to the tar
    /// reader is the location on disk rather than a route to it.
    /// </summary>
    /// <remarks>
    /// This is not cosmetic. <c>TarFile</c> checks that an entry cannot escape the
    /// destination by resolving that directory's links and then measuring the entry's
    /// unresolved path against the resolved prefix. Where the two differ in length the
    /// measurement is wrong, and on macOS they always differ: the temporary directory lives
    /// under <c>/var</c>, which is a link to <c>/private/var</c>. A short enough entry name —
    /// <c>share</c> is short enough — then reads past the end of its own path and the
    /// extraction dies with an <see cref="ArgumentOutOfRangeException"/>. Passing an already
    /// resolved directory makes the two paths the same and sidesteps it. Recent .NET 10
    /// servicing fixes the check itself, but a released CLI carries its own runtime, so the
    /// fix cannot be relied on here.
    /// </remarks>
    private static string ResolvePhysicalPath(string path)
    {
        string fullPath = Path.GetFullPath(path);

        if (Path.GetPathRoot(fullPath) is not { Length: > 0 } root)
        {
            return fullPath;
        }

        string current = root;

        foreach (string component in fullPath[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);

            // Only an existing entry can be a link, and the target is taken as written so a
            // link whose own target is missing still resolves.
            if (Path.Exists(current) &&
                new FileInfo(current).ResolveLinkTarget(returnFinalTarget: true) is { } target)
            {
                current = target.FullName;
            }
        }

        return current;
    }
}
