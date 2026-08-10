using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.Messaging;

namespace Share.Application.Configuration.Create;

/// <summary>
/// Adds a workspace to the configuration file and makes it active, so the settings that
/// follow are written into it rather than into whichever workspace was in use before.
/// </summary>
/// <param name="Name">Name of the workspace to add, e.g. <c>development</c>.</param>
/// <param name="Settings">
/// What to write into the new workspace, or <see langword="null"/> to create it empty and
/// leave every setting defaulted. Filling it in here rather than following the create with
/// a separate <c>config set</c> keeps a half-configured workspace off the disk.
/// </param>
public sealed record CreateWorkspaceCommand(string Name, ShareApiSettings? Settings = null)
    : ICommand<ConfigurationResponse>;
