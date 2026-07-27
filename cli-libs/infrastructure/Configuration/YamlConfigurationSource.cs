using Microsoft.Extensions.Configuration;
using YamlDotNet.Core;

namespace Share.Infrastructure.Configuration;

/// <summary>
/// A YAML file as a configuration source. Behaves like the built-in JSON source —
/// <c>Optional</c>, <c>ReloadOnChange</c> and file-provider resolution all come from
/// <see cref="FileConfigurationSource"/>.
/// </summary>
internal sealed class YamlConfigurationSource : FileConfigurationSource
{
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);

        return new YamlConfigurationProvider(this);
    }
}

internal sealed class YamlConfigurationProvider(YamlConfigurationSource source)
    : FileConfigurationProvider(source)
{
    public override void Load(Stream stream)
    {
        try
        {
            Data = YamlConfigurationParser.Parse(stream);
        }
        catch (YamlException exception)
        {
            throw new FormatException(
                $"Could not parse the YAML configuration file: {exception.Message}",
                exception);
        }
    }
}
