using Application.Abstractions.Messaging;

namespace Application.Shares.GetById;

public sealed record GetShareByIdQuery(Guid ShareId) : IQuery<ShareResponse>;
