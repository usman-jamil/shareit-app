using Share.Application.Abstractions.Configuration;
using Share.Domain.Configuration;

namespace Share.Application.Configuration.List;

/// <summary>
/// The workspaces the configuration file defines.
/// </summary>
/// <param name="Location">Absolute path of the configuration file.</param>
/// <param name="Exists">Whether that file is present.</param>
/// <param name="Active">The workspace reads and writes currently go to.</param>
/// <param name="Workspaces">The defined workspaces, in the order the file holds them.</param>
/// <param name="ActiveIsMissing">
/// Whether <paramref name="Active"/> names a workspace the file does not define — a
/// hand-edited file can, and this listing is how the user is told so.
/// </param>
public sealed record WorkspacesResponse(
    string Location,
    bool Exists,
    string Active,
    IReadOnlyList<WorkspaceView> Workspaces,
    bool ActiveIsMissing)
{
    public static WorkspacesResponse From(string location, bool exists, WorkspaceList workspaces)
    {
        ArgumentNullException.ThrowIfNull(workspaces);

        return new WorkspacesResponse(
            location,
            exists,
            workspaces.Active,
            [.. workspaces.Workspaces.Select(workspace => WorkspaceView.From(workspace, workspaces.Active))],
            !workspaces.Workspaces.Any(workspace =>
                ConfigurationWorkspaces.NameComparer.Equals(workspace.Name, workspaces.Active)));
    }
}
