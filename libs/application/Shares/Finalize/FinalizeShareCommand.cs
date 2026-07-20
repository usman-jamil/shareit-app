using Application.Abstractions.Messaging;

namespace Application.Shares.Finalize;

public sealed record FinalizeShareCommand(Guid ShareId) : ICommand;
