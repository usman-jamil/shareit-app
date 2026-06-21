using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Files;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Files.GetById;

internal sealed class GetFileByIdQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetFileByIdQuery, FileResponse>
{
    public async Task<Result<FileResponse>> Handle(GetFileByIdQuery query, CancellationToken cancellationToken)
    {
        FileResponse? file = await context.Files
          .Where(file => file.Id == query.FileId)
          .Select(file => new FileResponse
          {
              Id = file.Id,
              ShareId = file.ShareId,
              RelativePath = file.RelativePath,
              Sha256 = file.Sha256,
              ContentType = file.ContentType,
              CreatedAt = file.CreatedAt,
              UpdatedAt = file.UpdatedAt,
              Size = file.Size
          })
          .SingleOrDefaultAsync(cancellationToken);

        if (file is null)
        {
            return Result.Failure<FileResponse>(FileErrors.NotFound(query.FileId));
        }

        return file;
    }
}
