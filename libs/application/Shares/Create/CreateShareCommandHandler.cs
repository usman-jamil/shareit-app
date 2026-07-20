using System.Security.Cryptography;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Files.Create;
using Domain.Files;
using Domain.Shares;
using Domain.Users;
using SharedKernel;

namespace Application.Shares.Create;

public sealed class CreateShareCommandHandler(
    IShareRepository shareRepository,
    IFileRepository fileRepository,
    IUserRepository userRepository,
    IStorageService storageService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateShareCommand, CreateShareResponse>
{
    // How long the presigned upload URLs remain valid for the client to PUT to.
    private static readonly TimeSpan UploadUrlLifetime = TimeSpan.FromHours(1);

    public async Task<Result<CreateShareResponse>> Handle(
        CreateShareCommand command,
        CancellationToken cancellationToken)
    {
        User? owner = await userRepository.GetByIdAsync(command.OwnerUserId, cancellationToken);

        if (owner is null)
        {
            return Result.Failure<CreateShareResponse>(UserErrors.NotFound(command.OwnerUserId));
        }

        DateTime now = dateTimeProvider.UtcNow;

        var share = new Share
        {
            OwnerUserId = owner.Id,
            Status = ShareStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddMinutes(command.ConfiguredTtlMinutes),
            ConfiguredTtlMinutes = command.ConfiguredTtlMinutes,
            TotalBytes = command.Files.Sum(file => (long)file.Size),
            FileCount = command.Files.Count
        };

        var uploadUrls = new List<FileUploadUrl>(command.Files.Count);

        foreach (FileUpload upload in command.Files)
        {
            var file = new Domain.Files.File
            {
                ShareId = share.Id,
                RelativePath = upload.RelativePath,
                // The real content hash is unknown until the client uploads; use a
                // random placeholder while the share is pending.
                Sha256 = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                ContentType = upload.ContentType,
                Size = upload.Size,
                CreatedAt = now,
                UpdatedAt = now
            };

            string key = BuildObjectKey(share.Id, upload.RelativePath);

            string url = await storageService.GeneratePresignedUploadUrlAsync(
                key,
                UploadUrlLifetime,
                cancellationToken);

            fileRepository.Add(file);
            uploadUrls.Add(new FileUploadUrl(upload.RelativePath, new Uri(url)));
        }

        shareRepository.Add(share);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateShareResponse
        {
            ShareId = share.Id,
            Files = uploadUrls
        };
    }

    private static string BuildObjectKey(Guid shareId, string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/').TrimStart('/');
        return $"{shareId}/{normalized}";
    }
}
