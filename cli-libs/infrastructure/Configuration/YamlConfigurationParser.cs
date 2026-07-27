using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace Share.Infrastructure.Configuration;

/// <summary>
/// Flattens a YAML document into the flat <c>Section:Key</c> pairs
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> works in, so
/// <c>shareApi: { baseUrl: ... }</c> becomes <c>ShareApi:BaseUrl</c>. Used both by the
/// configuration provider and by the store that reads the file directly.
/// </summary>
internal static class YamlConfigurationParser
{
    public static IDictionary<string, string?> Parse(Stream stream)
    {
        using var reader = new StreamReader(stream);

        return Parse(reader);
    }

    public static IDictionary<string, string?> Parse(TextReader reader)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var yaml = new YamlStream();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0)
        {
            return data;
        }

        Visit(data, prefix: null, yaml.Documents[0].RootNode);

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
