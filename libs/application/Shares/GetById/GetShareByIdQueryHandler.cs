using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Shares;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Shares.GetById;

internal sealed class GetShareByIdQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetShareByIdQuery, ShareResponse>
{
    public async Task<Result<ShareResponse>> Handle(GetShareByIdQuery query, CancellationToken cancellationToken)
    {
        ShareResponse? share = await context.Shares
          .Where(share => share.Id == query.ShareId)
          .Select(share => new ShareResponse
          {
              Id = share.Id,
              OwnerUserId = share.OwnerUserId,
              Status = share.Status,
              CreatedAt = share.CreatedAt,
              UpdatedAt = share.UpdatedAt,
              ExpiresAt = share.ExpiresAt,
              ConfiguredTtlMinutes = share.ConfiguredTtlMinutes,
              TotalBytes = share.TotalBytes,
              FileCount = share.FileCount
          })
          .SingleOrDefaultAsync(cancellationToken);

        if (share is null)
        {
            return Result.Failure<ShareResponse>(ShareErrors.NotFound(query.ShareId));
        }

        return share;
    }
}
