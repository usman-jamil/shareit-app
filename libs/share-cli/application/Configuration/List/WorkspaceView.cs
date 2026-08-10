using Share.Application.Abstractions.Configuration;
using Share.Domain.Configuration;

namespace Share.Application.Configuration.List;

/// <summary>
/// One row of <c>share config list</c>: which server a workspace points at, and whether it
/// is the one in force.
/// </summary>
/// <param name="Name">The workspace's name, as the file holds it.</param>
/// <param name="BaseUrl">
/// The server the workspace would reach — the value it sets, or the default it falls back
/// to. A string rather than a <see cref="Uri"/> because a hand-edited file can hold
/// something that is not one, and showing it as written is what lets the user spot that.
/// </param>
/// <param name="BaseUrlIsDefault">Whether the workspace sets no base URL of its own.</param>
/// <param name="IsActive">Whether reads and writes currently go here.</param>
public sealed record WorkspaceView(
    string Name,
    string BaseUrl,
    bool BaseUrlIsDefault,
    bool IsActive)
{
    public static WorkspaceView From(WorkspaceSummary workspace, string active)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        return new WorkspaceView(
            workspace.Name,
            workspace.BaseUrl ?? ShareApiDefaults.BaseUrl.ToString(),
            workspace.BaseUrl is null,
            ConfigurationWorkspaces.NameComparer.Equals(workspace.Name, active));
    }
}
