namespace Share.Infrastructure.Configuration;

/// <summary>
/// Resolves where the CLI's configuration file lives: <c>&lt;user home&gt;/.share/config.yaml</c>.
/// </summary>
/// <remarks>
/// The home directory comes from <see cref="Environment.SpecialFolder.UserProfile"/>, which
/// the runtime maps per platform — <c>C:\Users\&lt;name&gt;</c> on Windows,
/// <c>/Users/&lt;name&gt;</c> on macOS, <c>/home/&lt;name&gt;</c> on Linux — and
/// <see cref="Path.Combine(string, string, string)"/> joins it with the platform's own
/// separator. Nothing here is OS-specific.
/// </remarks>
public static class CliConfigurationPath
{
    /// <summary>
    /// Overrides the resolved path entirely. Useful for tests and for running against a
    /// second environment without touching the real file.
    /// </summary>
    public const string OverrideEnvironmentVariable = "SHARE_CLI_CONFIG";

    private const string DirectoryName = ".share";
    private const string FileName = "config.yaml";

    public static string Resolve()
    {
        string? overridePath = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        return Path.Combine(ResolveHomeDirectory(), DirectoryName, FileName);
    }

    private static string ResolveHomeDirectory()
    {
        // DoNotVerify: the folder is still the right answer when it has yet to be created.
        string home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolderOption.DoNotVerify);

        if (!string.IsNullOrWhiteSpace(home))
        {
            return home;
        }

        // UserProfile comes back empty in some sandboxed and containerised environments.
        return Environment.GetEnvironmentVariable("HOME")
               ?? Environment.GetEnvironmentVariable("USERPROFILE")
               ?? Directory.GetCurrentDirectory();
    }
}
