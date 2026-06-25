using Application.Abstractions.Messaging;

namespace Application.ApiKeys.Get;

public sealed record GetApiKeyQuery(string ApiKey) : IQuery<ApiKeyResponse>;
