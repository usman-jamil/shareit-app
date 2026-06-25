using Application.Abstractions.Messaging;

namespace Application.ApiKeys.Create;

public sealed record CreateApiKeyCommand(Guid UserId, string? Label) : ICommand<CreateApiKeyResponse>;
