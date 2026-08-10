using SharedKernel;

namespace Share.Domain.Configuration;

/// <summary>
/// Failures reading or writing the CLI's configuration file.
/// </summary>
public static class ConfigurationErrors
{
    public static Error Unreadable(string path, string reason) => Error.Failure(
      "Configuration.Unreadable",
      $"The configuration file at '{path}' could not be read: {reason}");

    public static Error Unwritable(string path, string reason) => Error.Failure(
      "Configuration.Unwritable",
      $"The configuration file at '{path}' could not be written: {reason}");

    /// <summary>
    /// The file exists but is not valid YAML. Also blocks writes: a file we cannot parse may
    /// hold hand-written content worth more than the update being applied, so it is never
    /// overwritten silently.
    /// </summary>
    public static Error Unparseable(string path, string reason) => Error.Failure(
      "Configuration.Unparseable",
      $"The configuration file at '{path}' is not valid YAML: {reason}. " +
      "Fix or delete the file, then try again.");

    public static Error InvalidValue(string path, string key, string reason) => Error.Failure(
      "Configuration.InvalidValue",
      $"'{key}' in the configuration file at '{path}' is not valid: {reason}");

    /// <summary>
    /// A workspace was named that the file does not define. Also raised when
    /// <c>active_workspace</c> points at a section that is not there — falling back to
    /// defaults would quietly aim the CLI at a different server than the user asked for.
    /// </summary>
    public static Error WorkspaceNotFound(string path, string name) => Error.NotFound(
      "Configuration.WorkspaceNotFound",
      $"There is no workspace named '{name}' in the configuration file at '{path}'. " +
      "Run `share config list` to see the workspaces, or `share config create " +
      $"{name}` to add it.");

    public static Error WorkspaceAlreadyExists(string path, string name) => Error.Conflict(
      "Configuration.WorkspaceAlreadyExists",
      $"A workspace named '{name}' already exists in the configuration file at '{path}'. " +
      $"Run `share config activate {name}` to switch to it.");

    /// <summary>
    /// A command that would normally ask which workspace to use was run somewhere it cannot
    /// ask — a pipe, a script, CI, a terminal that cannot be drawn on. Naming one is the way
    /// out, so the message says which command to name it on.
    /// </summary>
    public static Error WorkspaceNameRequired(string command) => Error.Failure(
      "Configuration.WorkspaceNameRequired",
      $"Name the workspace, e.g. `share config {command} development`. " +
      "There is no interactive terminal here to ask on.");

    public static Error NoWorkspaces(string path) => Error.NotFound(
      "Configuration.NoWorkspaces",
      $"The configuration file at '{path}' defines no workspaces. " +
      "Run `share config create` to add one.");

    public static Error InvalidBaseUrl(string value) => Error.Failure(
      "Configuration.InvalidBaseUrl",
      $"'{value}' is not an absolute URL, e.g. https://api.example.com");
}
