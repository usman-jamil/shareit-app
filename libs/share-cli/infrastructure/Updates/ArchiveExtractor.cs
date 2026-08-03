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
                destinationDirectory,
                overwriteFiles: true,
                cancellationToken);

            return Result.Success();
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or NotSupportedException)
        {
            return Result.Failure(
                UpdateErrors.ArchiveUnreadable(Path.GetFileName(archivePath), exception.Message));
        }
    }
}
