namespace Share.Application.Abstractions.Configuration;

/// <summary>
/// Every workspace the configuration file defines, and which one is active.
/// </summary>
/// <param name="Active">
/// The active workspace. Normally one of <paramref name="Workspaces"/>, but not necessarily —
/// a hand-edited file can name a workspace it does not define, and listing has to keep
/// working precisely so the user can see that and fix it.
/// </param>
/// <param name="Workspaces">The defined workspaces, in the order the file holds them.</param>
public sealed record WorkspaceList(string Active, IReadOnlyList<WorkspaceSummary> Workspaces);
