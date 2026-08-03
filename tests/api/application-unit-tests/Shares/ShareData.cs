using Domain.Shares;

namespace Application.UnitTests.Shares;

internal static class ShareData
{
    public static Share Create() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        ShareStatus.Pending,
        DateTime.UtcNow,
        DateTime.UtcNow,
        DateTime.UtcNow,
        1,
        1,
        1
    );
}
