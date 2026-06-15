using Application.Abstractions.Messaging;

namespace Application.ApiKeys.Revoke;

public sealed record RevokeApiKeyCommand(Guid ApiKeyId) : ICommand;
