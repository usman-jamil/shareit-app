using Microsoft.Extensions.Configuration;

namespace Share.Infrastructure.Configuration;

public static class ShareCliConfigurationExtensions
{
    /// <summary>
    /// Adds the user's <c>~/.share/config.yaml</c> as a configuration source.
    /// </summary>
    /// <remarks>
    /// Add this <b>last</b>. The file is the CLI's source of truth, so it must sit above
    /// <c>appsettings.json</c>, user secrets and environment variables in precedence. The
    /// file is optional — a fresh install has no file and every setting defaults.
    /// </remarks>
    public static IConfigurationBuilder AddShareCliConfigurationFile(
        this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var source = new YamlConfigurationSource
        {
            Path = CliConfigurationPath.Resolve(),
            Optional = true,
            ReloadOnChange = false,
            OnLoadException = OnLoadException
        };

        // Turns the absolute path into a file provider rooted at its directory, the same
        // way AddJsonFile does.
        source.ResolveFileProvider();

        return builder.Add(source);
    }

    /// <summary>
    /// A broken configuration file must not stop the CLI from starting — otherwise the user
    /// cannot run <c>share config</c> to diagnose or fix it, and every command dies with a
    /// stack trace. Warn on stderr, carry on with defaults, and let <c>share config show</c>
    /// report the precise problem. Written directly to stderr because configuration is built
    /// before any logger exists.
    /// </summary>
    private static void OnLoadException(FileLoadExceptionContext context)
    {
        string reason = context.Exception.InnerException?.Message ?? context.Exception.Message;

        Console.Error.WriteLine(
            $"Warning: ignoring the configuration file because it could not be read. {reason}");
        Console.Error.WriteLine("Run `share config show` for details.");

        context.Ignore = true;
    }
}
