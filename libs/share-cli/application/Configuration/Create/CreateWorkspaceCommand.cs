using Share.Application.Abstractions.Messaging;

namespace Share.Application.Configuration.Create;

/// <summary>
/// Adds a workspace to the configuration file and makes it active, so the settings that
/// follow are written into it rather than into whichever workspace was in use before.
/// </summary>
/// <param name="Name">Name of the workspace to add, e.g. <c>development</c>.</param>
public sealed record CreateWorkspaceCommand(string Name) : ICommand<ConfigurationResponse>;
