using Share.Application.Abstractions.Api;
using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.FileSystem;
using Share.Application.Abstractions.Messaging;
using Share.Application.Abstractions.Progress;
using Share.Application.Abstractions.Storage;
using Share.Domain.Shares;
using SharedKernel;

namespace Share.Application.Shares.Create;

/// <summary>
/// Drives the three-step upload conversation for a whole directory: declare the manifest,
/// PUT every file to the presigned URL it came back with, then finalize.
/// </summary>
/// <remarks>
/// Nothing is rolled back on failure. A share that is created but never finalized stays
/// <c>pending</c> and expires on its own, so the CLI reports which step failed and stops
/// rather than trying to clean up server-side state it does not own.
/// </remarks>
internal sealed class CreateShareCommandHandler(
    IFileScanner scanner,
    IShareApiClient api,
    IFileUploader uploader,
    IConfigurationStore configurationStore)
    : ICommandHandler<CreateShareCommand, CreateShareResponse>
{
    public async Task<Result<CreateShareResponse>> Handle(
        CreateShareCommand command,
        CancellationToken cancellationToken)
    {
        Result<Guid> owner = await ResolveOwnerAsync(command, cancellationToken);

        if (owner.IsFailure)
        {
            return Result.Failure<CreateShareResponse>(owner.Error);
        }

        Result<ScannedDirectory> scanned = scanner.Scan(command.DirectoryPath);

        if (scanned.IsFailure)
        {
            return Result.Failure<CreateShareResponse>(scanned.Error);
        }

        IReadOnlyList<LocalFile> files = scanned.Value.Files;

        Result<CreateShareRequest> request = BuildRequest(owner.Value, command.TtlMinutes, files);

        if (request.IsFailure)
        {
            return Result.Failure<CreateShareResponse>(request.Error);
        }

        Result<CreatedShare> created = await api.CreateShareAsync(request.Value, cancellationToken);

        if (created.IsFailure)
        {
            return Result.Failure<CreateShareResponse>(created.Error);
        }

        long totalBytes = files.Sum(file => file.Size);

        // Reported only once the share exists: until then there is nothing to upload, and a
        // bar that appears before the API has agreed would have to be taken back again.
        IUploadProgressReporter progress = command.Progress ?? NullUploadProgressReporter.Instance;

        progress.Starting(files.Count, totalBytes);

        Result uploaded = await UploadAllAsync(files, created.Value, progress, cancellationToken);

        if (uploaded.IsFailure)
        {
            return Result.Failure<CreateShareResponse>(uploaded.Error);
        }

        Result finalized = await api.FinalizeShareAsync(created.Value.ShareId, cancellationToken);

        return finalized.IsFailure
            ? Result.Failure<CreateShareResponse>(finalized.Error)
            : Result.Success(new CreateShareResponse(
                created.Value.ShareId,
                scanned.Value.Root,
                files.Count,
                totalBytes));
    }

    /// <summary>
    /// The command wins over the configured user id, so a single run can be redirected
    /// without rewriting the configuration file.
    /// </summary>
    private async Task<Result<Guid>> ResolveOwnerAsync(
        CreateShareCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OwnerUserId is { } explicitOwner)
        {
            return Result.Success(explicitOwner);
        }

        // Reads the active workspace, so `share create` follows `share config activate`
        // without the command line saying anything about workspaces.
        Result<ActiveWorkspace> workspace = await configurationStore.ReadAsync(cancellationToken);

        if (workspace.IsFailure)
        {
            return Result.Failure<Guid>(workspace.Error);
        }

        return workspace.Value.Settings.UserId is { } configuredOwner
            ? Result.Success(configuredOwner)
            : Result.Failure<Guid>(ShareErrors.OwnerNotConfigured());
    }

    private static Result<CreateShareRequest> BuildRequest(
        Guid ownerUserId,
        int? ttlMinutes,
        IReadOnlyList<LocalFile> files)
    {
        var manifest = new List<FileUploadRequest>(files.Count);

        foreach (LocalFile file in files)
        {
            // The API's manifest carries sizes as a 32-bit value. Catching this here means
            // an oversized file is reported by name instead of coming back as a validation
            // failure about a negative number.
            if (file.Size > int.MaxValue)
            {
                return Result.Failure<CreateShareRequest>(
                    ShareErrors.FileTooLarge(file.RelativePath, file.Size));
            }

            manifest.Add(new FileUploadRequest(file.RelativePath, (int)file.Size, file.ContentType));
        }

        return Result.Success(new CreateShareRequest(ownerUserId, ttlMinutes, manifest));
    }

    /// <summary>
    /// Uploads one file at a time and stops at the first failure: the share cannot be
    /// finalized without every file, so there is nothing to gain by continuing.
    /// </summary>
    private async Task<Result> UploadAllAsync(
        IReadOnlyList<LocalFile> files,
        CreatedShare created,
        IUploadProgressReporter progress,
        CancellationToken cancellationToken)
    {
        var targets = created.Files.ToDictionary(
            target => target.RelativePath,
            target => target.UploadUrl,
            StringComparer.Ordinal);

        // One adapter for the whole run: it holds no state of its own, and the reporter is
        // told which file the counts belong to by FileStarting.
        var bytesUploaded = new FileUploadProgress(progress);

        foreach (LocalFile file in files)
        {
            // Matched by relative path rather than by position — the API makes no promise
            // about the order it returns targets in.
            if (!targets.TryGetValue(file.RelativePath, out Uri? uploadUrl))
            {
                return Result.Failure(ShareErrors.MissingUploadUrl(file.RelativePath));
            }

            progress.FileStarting(file.RelativePath, file.Size);

            Result uploaded =
                await uploader.UploadAsync(uploadUrl, file, bytesUploaded, cancellationToken);

            if (uploaded.IsFailure)
            {
                return uploaded;
            }

            progress.FileCompleted(file.RelativePath);
        }

        return Result.Success();
    }
}
