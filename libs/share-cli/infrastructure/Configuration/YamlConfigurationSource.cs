using Microsoft.Extensions.Configuration;
using Share.Infrastructure.Options;
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

/// <summary>
/// Surfaces the <b>active workspace only</b>, under the <c>ShareApi</c> section that
/// <see cref="ShareApiOptions"/> binds to. The other workspaces are deliberately not
/// exposed: nothing binds them, and keeping them out means an inactive workspace's API key
/// never reaches <c>IConfiguration</c> at all.
/// </summary>
internal sealed class YamlConfigurationProvider(YamlConfigurationSource source)
    : FileConfigurationProvider(source)
{
    public override void Load(Stream stream)
    {
        try
        {
            using var reader = new StreamReader(stream);

            var document = WorkspaceDocument.Load(reader);

            // An active workspace the file does not define yields no values, so every
            // setting defaults. Startup must not fail over it — `share config show` reports
            // it properly, and any command that talks to the API fails on the way out.
            Data = YamlConfigurationParser.Flatten(
                document.Read(document.ActiveWorkspace),
                ShareApiOptions.SectionName);
        }
        catch (YamlException exception)
        {
            throw new FormatException(
                $"Could not parse the YAML configuration file: {exception.Message}",
                exception);
        }
    }
}
