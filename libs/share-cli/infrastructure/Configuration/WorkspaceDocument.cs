using Share.Domain.Configuration;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Share.Infrastructure.Configuration;

/// <summary>
/// The configuration file's shape: a root-level <c>active_workspace</c> key alongside one
/// section per workspace. The single place that knows that shape — the store writes
/// through it and the configuration provider reads through it, so the two can never
/// disagree about which section is in force.
/// </summary>
internal sealed class WorkspaceDocument
{
    private static readonly YamlScalarNode ActiveKeyNode =
        new(ConfigurationWorkspaces.ActiveKey);

    private readonly YamlMappingNode _root;

    private WorkspaceDocument(YamlMappingNode root) => _root = root;

    /// <summary>
    /// The workspace every read and write acts on. Falls back to the default when the file
    /// names none, which is what makes a pre-workspace file valid as it stands.
    /// </summary>
    public string ActiveWorkspace =>
        _root.Children.TryGetValue(ActiveKeyNode, out YamlNode? node) &&
        node is YamlScalarNode { Value: { } value } &&
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : ConfigurationWorkspaces.DefaultName;

    /// <summary>
    /// The defined workspaces, in file order. The default workspace is always listed even
    /// when the file has no section for it, because it is always selectable.
    /// </summary>
    public IReadOnlyList<string> Workspaces
    {
        get
        {
            List<string> names = [.. Sections().Select(section => section.Name)];

            if (!names.Contains(ConfigurationWorkspaces.DefaultName, ConfigurationWorkspaces.NameComparer))
            {
                names.Insert(0, ConfigurationWorkspaces.DefaultName);
            }

            return names;
        }
    }

    public static WorkspaceDocument Empty() => new(Block());

    /// <summary>
    /// Parses a document. Anything that is not a mapping at the root is treated as empty
    /// rather than rejected — an empty file is a legitimate starting point.
    /// </summary>
    /// <exception cref="YamlDotNet.Core.YamlException">The content is not valid YAML.</exception>
    public static WorkspaceDocument Load(TextReader reader)
    {
        var yaml = new YamlStream();
        yaml.Load(reader);

        return new WorkspaceDocument(
            yaml.Documents.Count > 0 && yaml.Documents[0].RootNode is YamlMappingNode mapping
                ? mapping
                : Block());
    }

    /// <summary>
    /// Whether the workspace can be selected. True for the default workspace whether or not
    /// the file has a section for it.
    /// </summary>
    public bool Contains(string name) =>
        ConfigurationWorkspaces.NameComparer.Equals(name, ConfigurationWorkspaces.DefaultName) ||
        Sections().Any(section => ConfigurationWorkspaces.NameComparer.Equals(section.Name, name));

    /// <summary>
    /// The workspace's section, or an empty mapping when the file does not define one. An
    /// absent section means "sets nothing", not "does not exist".
    /// </summary>
    public YamlMappingNode Read(string name) =>
        Sections()
            .Where(section => ConfigurationWorkspaces.NameComparer.Equals(section.Name, name))
            .Select(section => section.Node)
            .FirstOrDefault() ?? Block();

    /// <summary>
    /// The workspace's section, added to the document if it is not already there. Reuses
    /// the existing key so the file keeps the casing the user wrote.
    /// </summary>
    public YamlMappingNode GetOrAdd(string name)
    {
        foreach ((string existing, YamlMappingNode node) in Sections())
        {
            if (ConfigurationWorkspaces.NameComparer.Equals(existing, name))
            {
                return node;
            }
        }

        YamlMappingNode created = Block();
        _root.Children.Add(new YamlScalarNode(name), created);

        return created;
    }

    public void SetActive(string name)
    {
        _root.Children.Remove(ActiveKeyNode);
        _root.Children.Add(ActiveKeyNode, new YamlScalarNode(name));
    }

    /// <summary>
    /// Renders the document with <c>active_workspace</c> first, so the file opens with the
    /// one line that says which of the sections below it is in force.
    /// </summary>
    public YamlMappingNode ToYaml()
    {
        YamlMappingNode ordered = Block();

        if (_root.Children.TryGetValue(ActiveKeyNode, out YamlNode? active))
        {
            ordered.Children.Add(ActiveKeyNode, active);
        }

        foreach (KeyValuePair<YamlNode, YamlNode> child in _root.Children)
        {
            if (child.Key.Equals(ActiveKeyNode))
            {
                continue;
            }

            // A section is forced back to block style on every write. A workspace is created
            // empty, an empty mapping can only be written as `{}`, and reading that back
            // gives a flow node that would otherwise stay flow once it has settings in it.
            if (child.Value is YamlMappingNode section)
            {
                section.Style = MappingStyle.Block;
            }

            ordered.Children.Add(child.Key, child.Value);
        }

        return ordered;
    }

    /// <summary>
    /// A mapping that renders as indented block YAML rather than <c>{a: 1, b: 2}</c> — the
    /// file is meant to be read and hand-edited, and a node built in code defaults to flow
    /// style.
    /// </summary>
    private static YamlMappingNode Block() => new() { Style = MappingStyle.Block };

    /// <summary>
    /// The root-level sections, which is exactly the set of workspaces: a scalar at the
    /// root is a setting of the file itself (only <c>active_workspace</c> today), and a
    /// mapping is a workspace.
    /// </summary>
    private IEnumerable<(string Name, YamlMappingNode Node)> Sections() =>
        _root.Children
            .Where(child =>
                child.Key is YamlScalarNode { Value: not null } &&
                !child.Key.Equals(ActiveKeyNode) &&
                child.Value is YamlMappingNode)
            .Select(child => (
                Name: ((YamlScalarNode)child.Key).Value!,
                Node: (YamlMappingNode)child.Value));
}
