namespace Share.Domain.Configuration;

/// <summary>
/// The rules for the named workspaces the configuration file is divided into. Each
/// workspace is one root-level section holding a complete set of API settings, so a single
/// CLI can point at several servers and switch between them.
/// </summary>
/// <remarks>
/// <see cref="DefaultName"/> is the section the file has always had, so an existing
/// single-server file is already a valid one-workspace file and needs no migration.
/// </remarks>
public static class ConfigurationWorkspaces
{
    /// <summary>
    /// The workspace used when the file names no other one. It always exists, even when the
    /// file has no section for it — an absent section simply means every setting defaults.
    /// </summary>
    public const string DefaultName = "shareApi";

    /// <summary>
    /// Root-level key naming the workspace every read and write goes to. Reserved: it sits
    /// alongside the workspaces rather than being one.
    /// </summary>
    public const string ActiveKey = "active_workspace";

    /// <summary>
    /// The maximum name length. Nothing technical forces it — it keeps `config list`
    /// readable and a name this long is a mistake rather than an intention.
    /// </summary>
    public const int MaximumNameLength = 64;

    /// <summary>
    /// Workspace names are matched the way <c>IConfiguration</c> matches section names, so
    /// <c>Development</c> and <c>development</c> are the same workspace rather than two that
    /// silently shadow each other.
    /// </summary>
    public static StringComparer NameComparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Whether <paramref name="name"/> may be used as a workspace name. The restriction is
    /// deliberate: the name becomes a YAML key and a section name in
    /// <c>IConfiguration</c>, and characters like <c>:</c> or a leading digit are legal in
    /// one and meaningless in the other.
    /// </summary>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumNameLength)
        {
            return false;
        }

        if (NameComparer.Equals(name, ActiveKey))
        {
            return false;
        }

        return char.IsAsciiLetter(name[0]) &&
               name.All(character =>
                   char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }
}
