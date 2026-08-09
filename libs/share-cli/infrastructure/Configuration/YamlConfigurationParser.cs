using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace Share.Infrastructure.Configuration;

/// <summary>
/// Flattens a YAML node into the flat <c>Section:Key</c> pairs
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> works in. Used both by
/// the configuration provider — which flattens the active workspace under
/// <c>ShareApi</c> — and by the store, which reads the same node with no prefix at all, so
/// both see identical values.
/// </summary>
internal static class YamlConfigurationParser
{
    public static IDictionary<string, string?> Flatten(YamlNode node, string? prefix = null)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        Visit(data, prefix, node);

        return data;
    }

    private static void Visit(IDictionary<string, string?> data, string? prefix, YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
                {
                    if (entry.Key is YamlScalarNode { Value: { } key })
                    {
                        Visit(data, Combine(prefix, key), entry.Value);
                    }
                }

                break;

            case YamlSequenceNode sequence:
                for (int index = 0; index < sequence.Children.Count; index++)
                {
                    Visit(
                        data,
                        Combine(prefix, index.ToString(CultureInfo.InvariantCulture)),
                        sequence.Children[index]);
                }

                break;

            case YamlScalarNode scalar when prefix is not null:
                // Last one wins, matching how the JSON provider treats duplicate keys.
                data[prefix] = scalar.Value;
                break;

            default:
                break;
        }
    }

    private static string Combine(string? prefix, string key) =>
        prefix is null
            ? key
            : prefix + Microsoft.Extensions.Configuration.ConfigurationPath.KeyDelimiter + key;
}
