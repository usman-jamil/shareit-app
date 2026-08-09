using Share.Application.Abstractions.Messaging;

namespace Share.Application.Configuration.Activate;

/// <summary>
/// Points the CLI at an existing workspace. Every later read and write goes to it, so this
/// is how one CLI moves between servers.
/// </summary>
/// <param name="Name">Name of the workspace to switch to.</param>
public sealed record ActivateWorkspaceCommand(string Name) : ICommand<ConfigurationResponse>;
