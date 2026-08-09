using SharedKernel;

namespace Share.Application.Abstractions.Configuration;

/// <summary>
/// Read/write access to the CLI's own configuration file — the source of truth for how
/// the CLI reaches the API. Where that file lives and what format it is in are
/// Infrastructure concerns.
/// </summary>
/// <remarks>
/// The file is divided into named workspaces, one per server the CLI can be pointed at.
/// <see cref="ReadAsync"/> and <see cref="SaveAsync"/> always act on the active one, so a
/// caller that does not care about workspaces never has to name one.
/// </remarks>
public interface IConfigurationStore
{
    /// <summary>
    /// Absolute path of the configuration file, whether or not it exists yet.
    /// </summary>
    string Location { get; }

    /// <summary>
    /// Whether the file is present. A missing file is not an error — it means every
    /// setting is defaulted.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Reads the active workspace and the settings it sets. Unset settings come back
    /// <see langword="null"/>; a missing file yields the default workspace with all-null
    /// settings rather than a failure. Fails if the file names an active workspace it does
    /// not define.
    /// </summary>
    Task<Result<ActiveWorkspace>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the settings into the active workspace, creating the file and its directory
    /// if needed. Settings left <see langword="null"/> are removed from the workspace so
    /// they fall back to defaults. Every other workspace, and any unrelated content in the
    /// file, is preserved.
    /// </summary>
    Task<Result> SaveAsync(ShareApiSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the workspaces the file defines and reports which is active. A missing file
    /// yields the default workspace alone.
    /// </summary>
    Task<Result<WorkspaceList>> ListWorkspacesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an empty workspace and makes it active, so subsequent writes land in it. Fails
    /// if a workspace of that name already exists.
    /// </summary>
    Task<Result> CreateWorkspaceAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Points the CLI at an existing workspace. Fails if there is no such workspace —
    /// creating one implicitly would hide a typo behind a set of silently defaulted
    /// settings.
    /// </summary>
    Task<Result> ActivateWorkspaceAsync(string name, CancellationToken cancellationToken = default);
}
