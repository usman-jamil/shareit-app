using System.Collections;

namespace AppImage.Host.Configuration;

/// <summary>
/// Translates the container's <c>APP_IMAGE_*</c> environment variables into the configuration keys
/// <see cref="AppImageOptions"/> binds to.
/// <para>
/// ASP.NET Core already understands <c>AppImage__Api__Destination</c>; these aliases exist because
/// the deployment contract documented in the README is the flat, shell-friendly spelling. They are
/// added as a configuration source, so they layer over <c>appsettings.json</c> in the usual way.
/// </para>
/// </summary>
internal static class EnvironmentConfiguration
{
    private static readonly Dictionary<string, string> KeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["APP_IMAGE_WEB_ROOT"] = "AppImage:WebRoot",
        ["APP_IMAGE_API_DESTINATION"] = "AppImage:Api:Destination",
        ["APP_IMAGE_API_PATH_PREFIX"] = "AppImage:Api:PathPrefix",
        ["APP_IMAGE_API_STRIP_PATH_PREFIX"] = "AppImage:Api:StripPathPrefix",
        ["APP_IMAGE_API_HEALTH_PATH"] = "AppImage:Api:HealthPath",
        ["APP_IMAGE_API_HEALTH_TIMEOUT_SECONDS"] = "AppImage:Api:HealthTimeoutSeconds",
        ["APP_IMAGE_TRUST_ALL_PROXIES"] = "AppImage:ForwardedHeaders:TrustAllProxies",
    };

    public static IEnumerable<KeyValuePair<string, string?>> Read(IDictionary variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var mapped = new List<KeyValuePair<string, string?>>(KeyMap.Count);

        foreach (DictionaryEntry entry in variables)
        {
            if (entry.Key is not string name || !KeyMap.TryGetValue(name, out string? key))
            {
                continue;
            }

            string? value = entry.Value as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                mapped.Add(new KeyValuePair<string, string?>(key, value));
            }
        }

        return mapped;
    }
}
