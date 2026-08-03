using Application.Files.Create;

namespace Application.Shares.Create;

public sealed class CreateShareResponse
{
    public Guid ShareId { get; set; }

    /// <summary>
    /// One presigned upload URL per file path supplied in the request. The client
    /// uploads directly to these; the share stays <c>pending</c> until finalised.
    /// </summary>
    public IReadOnlyCollection<FileUploadUrl> Files { get; set; } = [];
}
