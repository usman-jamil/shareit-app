using System.Security.Cryptography;
using Share.Application.Abstractions.Updates;
using Share.Domain.Updates;
using SharedKernel;

namespace Share.Infrastructure.Updates;

/// <summary>
/// Downloads a release archive, checks it against the SHA-256 the release published,
/// unpacks it, and swaps the result in for the installed binary.
/// </summary>
/// <remarks>
/// Uses its own <see cref="HttpClient"/>, registered without
/// <see cref="Api.ApiKeyHeaderHandler"/> and with no timeout: these requests go to GitHub,
/// where the Share API key has no business being sent, and a ~35 MB download takes as long
/// as the connection takes — cancellation is what stops it.
/// </remarks>
internal sealed class UpdatePackageInstaller(
    HttpClient httpClient,
    IApplicationEnvironment environment)
    : IUpdatePackageInstaller
{
    private const string ExtractedDirectoryName = "extracted";

    /// <summary>
    /// The mode a replaced binary is given when the old one's could not be read. Owner
    /// writes, everyone runs — what an installed CLI normally has.
    /// </summary>
    private const UnixFileMode DefaultExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    public async Task<Result<StagedUpdate>> StageAsync(
        ReleaseInfo release,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);

        if (environment.RuntimeIdentifier is not { } runtimeIdentifier)
        {
            return Result.Failure<StagedUpdate>(
                UpdateErrors.UnsupportedPlatform(environment.PlatformDescription));
        }

        string archiveName = ReleasePackaging.ArchiveName(release.Version, runtimeIdentifier);

        if (Find(release, archiveName) is not { } archiveAsset)
        {
            return Result.Failure<StagedUpdate>(UpdateErrors.AssetNotFound(archiveName));
        }

        if (Find(release, ReleasePackaging.ChecksumsAssetName) is not { } checksumsAsset)
        {
            return Result.Failure<StagedUpdate>(UpdateErrors.ChecksumsUnavailable());
        }

        string directory;

        try
        {
            directory = UpdateWorkspace.CreateDirectory("package");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failure<StagedUpdate>(UpdateErrors.StagingFailed(exception.Message));
        }

        Result<StagedUpdate> staged = await StageIntoAsync(
            directory,
            archiveAsset,
            checksumsAsset,
            runtimeIdentifier,
            cancellationToken);

        if (staged.IsFailure)
        {
            UpdateWorkspace.TryDelete(directory);
        }

        return staged;
    }

    public async Task<Result> ReplaceAsync(
        StagedUpdate staged,
        string targetExecutablePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(staged);

        if (Path.GetDirectoryName(targetExecutablePath) is not { Length: > 0 } directory)
        {
            return Result.Failure(
                UpdateErrors.TargetNotWritable(targetExecutablePath, "it has no directory"));
        }

        // The replacement is written beside the target so the final step is a rename within
        // one volume: that is atomic, so an interrupted update leaves either the old binary
        // or the new one, never a half-copied file.
        string incoming = Path.Combine(directory, $".share-update-{Guid.NewGuid():N}.tmp");

        try
        {
            await CopyAsync(staged.ExecutablePath, incoming, cancellationToken);

            PreservePermissions(targetExecutablePath, incoming);

            Swap(incoming, targetExecutablePath);

            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteFile(incoming);

            return Result.Failure(
                UpdateErrors.TargetNotWritable(targetExecutablePath, exception.Message));
        }
        catch (OperationCanceledException)
        {
            TryDeleteFile(incoming);

            throw;
        }
    }

    public void Discard(StagedUpdate staged)
    {
        ArgumentNullException.ThrowIfNull(staged);

        UpdateWorkspace.TryDelete(staged.Directory);
    }

    private async Task<Result<StagedUpdate>> StageIntoAsync(
        string directory,
        ReleaseAsset archiveAsset,
        ReleaseAsset checksumsAsset,
        string runtimeIdentifier,
        CancellationToken cancellationToken)
    {
        // Checksums first: fetching them after the archive would mean a 35 MB download
        // could still turn out to be unverifiable.
        Result<string> checksums = await ReadTextAsync(checksumsAsset, cancellationToken);

        if (checksums.IsFailure)
        {
            return Result.Failure<StagedUpdate>(checksums.Error);
        }

        if (!Sha256Sums.TryFind(checksums.Value, archiveAsset.Name, out string expected))
        {
            return Result.Failure<StagedUpdate>(
                UpdateErrors.ChecksumMissing(archiveAsset.Name));
        }

        string archivePath = Path.Combine(directory, archiveAsset.Name);

        Result downloaded = await DownloadAsync(archiveAsset, archivePath, cancellationToken);

        if (downloaded.IsFailure)
        {
            return Result.Failure<StagedUpdate>(downloaded.Error);
        }

        Result<string> actual = await ComputeSha256Async(
            archivePath,
            archiveAsset.Name,
            cancellationToken);

        if (actual.IsFailure)
        {
            return Result.Failure<StagedUpdate>(actual.Error);
        }

        if (!string.Equals(actual.Value, expected, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<StagedUpdate>(
                UpdateErrors.ChecksumMismatch(archiveAsset.Name));
        }

        string extracted = Path.Combine(directory, ExtractedDirectoryName);

        Result unpacked = await ArchiveExtractor.ExtractAsync(
            archivePath,
            extracted,
            runtimeIdentifier,
            cancellationToken);

        if (unpacked.IsFailure)
        {
            return Result.Failure<StagedUpdate>(unpacked.Error);
        }

        string executableName = ReleasePackaging.ExecutableName(runtimeIdentifier);
        string executablePath = Path.Combine(extracted, executableName);

        if (!File.Exists(executablePath))
        {
            return Result.Failure<StagedUpdate>(
                UpdateErrors.ExecutableMissing(archiveAsset.Name, executableName));
        }

        return Result.Success(new StagedUpdate(executablePath, directory));
    }

    private async Task<Result<string>> ReadTextAsync(
        ReleaseAsset asset,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response =
                await httpClient.GetAsync(asset.DownloadUrl, cancellationToken);

            return response.IsSuccessStatusCode
                ? Result.Success(await response.Content.ReadAsStringAsync(cancellationToken))
                : Result.Failure<string>(
                    UpdateErrors.DownloadRejected(asset.Name, (int)response.StatusCode));
        }
        catch (HttpRequestException exception)
        {
            return Result.Failure<string>(
                UpdateErrors.DownloadFailed(asset.Name, exception.Message));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<string>(UpdateErrors.DownloadTimedOut(asset.Name));
        }
    }

    private async Task<Result> DownloadAsync(
        ReleaseAsset asset,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                asset.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure(
                    UpdateErrors.DownloadRejected(asset.Name, (int)response.StatusCode));
            }

            await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream file = File.Create(destinationPath);

            await content.CopyToAsync(file, cancellationToken);

            return Result.Success();
        }
        catch (HttpRequestException exception)
        {
            return Result.Failure(UpdateErrors.DownloadFailed(asset.Name, exception.Message));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(UpdateErrors.DownloadTimedOut(asset.Name));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failure(UpdateErrors.DownloadFailed(asset.Name, exception.Message));
        }
    }

    private static async Task<Result<string>> ComputeSha256Async(
        string path,
        string assetName,
        CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream file = File.OpenRead(path);

            byte[] hash = await SHA256.HashDataAsync(file, cancellationToken);

            return Result.Success(Convert.ToHexString(hash));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Failure<string>(
                UpdateErrors.ArchiveUnreadable(assetName, exception.Message));
        }
    }

    private static async Task CopyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using FileStream source = File.OpenRead(sourcePath);
        await using FileStream destination = File.Create(destinationPath);

        await source.CopyToAsync(destination, cancellationToken);
    }

    /// <summary>
    /// Carries the installed binary's mode onto its replacement, so an install that was
    /// made group-writable or root-owned-and-world-readable stays that way.
    /// </summary>
    private static void PreservePermissions(string targetPath, string incomingPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        UnixFileMode mode = DefaultExecutableMode;

        if (File.Exists(targetPath))
        {
            UnixFileMode existing = File.GetUnixFileMode(targetPath);

            // A target with no execute bit at all is not something to copy: it would leave
            // the user with a binary they cannot run.
            if (existing.HasFlag(UnixFileMode.UserExecute))
            {
                mode = existing;
            }
        }

        File.SetUnixFileMode(incomingPath, mode);
    }

    /// <summary>
    /// Moves the new binary onto the old one. On Unix that is a plain rename and works even
    /// while the old image is mapped. On Windows the old file may still be held open, in
    /// which case it is renamed aside first — Windows permits renaming a locked executable
    /// even though it forbids overwriting one.
    /// </summary>
    private static void Swap(string incomingPath, string targetPath)
    {
        try
        {
            File.Move(incomingPath, targetPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            string displaced = $"{targetPath}.old-{Guid.NewGuid():N}";

            File.Move(targetPath, displaced);

            try
            {
                File.Move(incomingPath, targetPath, overwrite: true);
            }
            catch
            {
                // Put the original back rather than leaving the user with no binary at all.
                File.Move(displaced, targetPath, overwrite: true);

                throw;
            }

            TryDeleteFile(displaced);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The displaced binary on Windows lands here while the old process still holds
            // it. It is beside the installed CLI rather than in a temp directory, so it is
            // named `.old-<id>` to make clear what it is.
        }
    }

    private static ReleaseAsset? Find(ReleaseInfo release, string name) =>
        release.Assets.FirstOrDefault(
            asset => string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase));
}
