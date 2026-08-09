using Share.Application.Abstractions.Messaging;

namespace Share.Application.Configuration.List;

/// <summary>
/// Reads the configuration file and reports the workspaces it defines.
/// </summary>
public sealed record ListWorkspacesQuery : IQuery<WorkspacesResponse>;
