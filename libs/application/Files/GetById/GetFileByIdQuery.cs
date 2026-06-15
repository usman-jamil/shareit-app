using Application.Abstractions.Messaging;

namespace Application.Files.GetById;

public sealed record GetFileByIdQuery(Guid FileId) : IQuery<FileResponse>;
