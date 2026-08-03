using Application.Abstractions.Messaging;
using Application.Files.Create;

namespace Application.Shares.Create;

public sealed record CreateShareCommand(
    Guid OwnerUserId,
    int ConfiguredTtlMinutes,
    IReadOnlyCollection<FileUpload> Files) : ICommand<CreateShareResponse>;
