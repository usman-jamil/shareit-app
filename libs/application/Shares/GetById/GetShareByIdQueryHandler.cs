using Application.Abstractions.Messaging;
using Application.Files.GetById;
using Domain.Files;
using Domain.Shares;
using SharedKernel;

namespace Application.Shares.GetById;

internal sealed class GetShareByIdQueryHandler(
  IShareRepository shareRepository,
  IFileRepository fileRepository)
  : IQueryHandler<GetShareByIdQuery, ShareResponse>
{
    public async Task<Result<ShareResponse>> Handle(GetShareByIdQuery query, CancellationToken cancellationToken)
    {
        Share? share = await shareRepository.GetByIdAsync(query.ShareId, cancellationToken);

        if (share is null || share.Status != ShareStatus.Finalized)
        {
            return Result.Failure<ShareResponse>(ShareErrors.NotFound(query.ShareId));
        }

        List<Domain.Files.File> files = await fileRepository.GetByShareIdAsync(share.Id, cancellationToken);

        var response = new ShareResponse
        {
            Id = share.Id,
            OwnerUserId = share.OwnerUserId,
            Status = share.Status,
            CreatedAt = share.CreatedAt,
            UpdatedAt = share.UpdatedAt,
            ExpiresAt = share.ExpiresAt,
            ConfiguredTtlMinutes = share.ConfiguredTtlMinutes,
            TotalBytes = share.TotalBytes,
            FileCount = share.FileCount,
            Files = [.. files.Select(file => new FileResponse
            {
                Id = file.Id,
                ShareId = file.ShareId,
                RelativePath = file.RelativePath,
                Sha256 = file.Sha256,
                ContentType = file.ContentType,
                CreatedAt = file.CreatedAt,
                UpdatedAt = file.UpdatedAt,
                Size = file.Size
            })]
        };

        return response;
    }
}
