using Share.Application.Abstractions.Api;
using Share.Domain.Shares;

namespace Share.Application.UnitTests.Api;

/// <summary>
/// Canned <see cref="IShareApiClient"/> outputs. Values are fixed rather than random so a
/// failing assertion names the same ids every run.
/// </summary>
internal static class ShareApiData
{
    public static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ShareId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid FileId = new("33333333-3333-3333-3333-333333333333");

    public static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public const string RelativePath = "docs/report.pdf";
    public const string ContentType = "application/pdf";
    public const int FileSize = 1024;
    public const int TtlMinutes = 60;

    public static UserDetails User(Guid? id = null) => new(
        id ?? UserId,
        "Usman",
        "test@test.com",
        CreatedAt);

    public static CreateShareRequest CreateRequest(Guid? ownerUserId = null) => new(
        ownerUserId ?? UserId,
        TtlMinutes,
        [new FileUploadRequest(RelativePath, FileSize, ContentType)]);

    public static CreatedShare CreatedShare(Guid? shareId = null) => new(
        shareId ?? ShareId,
        [new FileUploadTarget(RelativePath, new Uri($"https://uploads.example/{RelativePath}"))]);

    public static ShareDetails Share(Guid? shareId = null, string status = ShareStatus.Pending)
    {
        Guid id = shareId ?? ShareId;

        return new ShareDetails(
            id,
            UserId,
            status,
            CreatedAt,
            CreatedAt,
            CreatedAt.AddMinutes(TtlMinutes),
            TtlMinutes,
            FileSize,
            1,
            [File(id)]);
    }

    public static ShareFile File(Guid? shareId = null) => new(
        FileId,
        shareId ?? ShareId,
        RelativePath,
        "0000000000000000000000000000000000000000000000000000000000000000",
        ContentType,
        CreatedAt,
        CreatedAt,
        FileSize);
}
