using Application.Abstractions.Messaging;

namespace Application.ApiKeys.GetById;

public sealed record GetApiKeyByIdQuery(Guid ApiKeyId) : IQuery<ApiKeyResponse>;
