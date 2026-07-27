using Share.Api.Types;
using Share.Application.Abstractions.Api;
using CreateShareBody = Share.Api.Types.Request;

namespace Share.Infrastructure.Api;

/// <summary>
/// Generated wire types in, Application models out. Keeping the translation here is what
/// lets the API contract be regenerated without the Application layer noticing.
/// </summary>
internal static class ShareApiMappings
{
    public static CreateShareBody ToBody(this CreateShareRequest request) =>
        new()
        {
            OwnerUserId = request.OwnerUserId,
            ConfiguredTtlMinutes = request.ConfiguredTtlMinutes,
            Files = request.Files
                .Select(file => new FileUpload
                {
                    RelativePath = file.RelativePath,
                    Size = file.Size,
                    // contentType is optional in the contract, but the generator annotates
                    // it non-nullable; omitting it on the wire is what the API expects.
                    ContentType = file.ContentType!
                })
                .ToList()
        };

    public static UserDetails ToUserDetails(this UserResponse response) =>
        new(
            response.Id,
            response.Name ?? string.Empty,
            response.Email ?? string.Empty,
            response.CreatedAt);

    public static CreatedShare ToCreatedShare(this CreateShareResponse response) =>
        new(
            response.ShareId,
            response.Files?
                .Select(file => new FileUploadTarget(file.RelativePath, file.UploadUrl))
                .ToArray() ?? []);

    public static ShareDetails ToShareDetails(this ShareResponse response) =>
        new(
            response.Id,
            response.OwnerUserId,
            response.Status ?? string.Empty,
            response.CreatedAt,
            response.UpdatedAt,
            response.ExpiresAt,
            response.ConfiguredTtlMinutes,
            response.TotalBytes,
            response.FileCount,
            response.Files?
                .Select(file => file.ToShareFile())
                .ToArray() ?? []);

    private static ShareFile ToShareFile(this FileResponse response) =>
        new(
            response.Id,
            response.ShareId,
            response.RelativePath ?? string.Empty,
            response.Sha256 ?? string.Empty,
            response.ContentType ?? string.Empty,
            response.CreatedAt,
            response.UpdatedAt,
            response.Size);
}
