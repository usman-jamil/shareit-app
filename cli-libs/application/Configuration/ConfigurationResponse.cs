using Share.Application.Abstractions.Configuration;
using Share.Domain.Configuration;

namespace Share.Application.Configuration;

/// <summary>
/// The effective configuration: what the CLI will actually use, plus enough provenance to
/// tell the user which values they have set and which are falling back to a default.
/// </summary>
/// <param name="Location">Absolute path of the configuration file.</param>
/// <param name="Exists">Whether that file is present.</param>
/// <param name="ApiKeyIsSet">
/// Whether an API key is configured. The key itself deliberately never leaves the store —
/// this response is printed to the console, so it carries presence only. There is no
/// "is default" counterpart because an API key has no default to fall back to.
/// </param>
/// <param name="UserId">
/// The configured owner for new shares, or <see langword="null"/> when none is set. Not a
/// secret, so unlike the API key it is reported in full.
/// </param>
public sealed record ConfigurationResponse(
    string Location,
    bool Exists,
    Uri BaseUrl,
    bool BaseUrlIsDefault,
    int TimeoutSeconds,
    bool TimeoutSecondsIsDefault,
    bool ApiKeyIsSet,
    Guid? UserId)
{
    public static ConfigurationResponse From(string location, bool exists, ShareApiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new ConfigurationResponse(
            location,
            exists,
            settings.BaseUrl ?? ShareApiDefaults.BaseUrl,
            settings.BaseUrl is null,
            settings.TimeoutSeconds ?? ShareApiDefaults.TimeoutSeconds,
            settings.TimeoutSeconds is null,
            !string.IsNullOrWhiteSpace(settings.ApiKey),
            settings.UserId);
    }
}
