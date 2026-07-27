using Share.Application.Abstractions.Messaging;

namespace Share.Application.Ping;

// Placeholder use case that exercises the CQRS seam end-to-end.
// Delete it once the first real use case lands.
public sealed record PingQuery : IQuery<string>;
