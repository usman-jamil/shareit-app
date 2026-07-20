using Application.Abstractions.Messaging;
using Domain.Shares;
using SharedKernel;

namespace Application.Shares.Finalize;

internal sealed class FinalizeShareCommandHandler(
    IShareRepository shareRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<FinalizeShareCommand>
{
    public async Task<Result> Handle(
        FinalizeShareCommand command,
        CancellationToken cancellationToken)
    {
        Share? share = await shareRepository.GetByIdAsync(command.ShareId, cancellationToken);

        if (share is null)
        {
            return Result.Failure(ShareErrors.NotFound(command.ShareId));
        }

        if (share.Status == ShareStatus.Finalized)
        {
            return Result.Failure(ShareErrors.AlreadyFinalized(command.ShareId));
        }

        share.Status = ShareStatus.Finalized;
        share.UpdatedAt = dateTimeProvider.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
