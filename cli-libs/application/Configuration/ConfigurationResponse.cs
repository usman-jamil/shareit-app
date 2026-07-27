using Share.Application.Abstractions.Configuration;
using Share.Domain.Configuration;

namespace Share.Application.Configuration;

/// <summary>
/// The effective configuration: what the CLI will actually use, plus enough provenance to
/// tell the user which values they have set and which are falling back to a default.
/// </summary>
/// <param name="Location">Absolute path of the configuration file.</param>
/// <param name="Exists">Whether that file is present.</param>
public sealed record ConfigurationResponse(
    string Location,
    bool Exists,
    Uri BaseUrl,
    bool BaseUrlIsDefault,
    int TimeoutSeconds,
    bool TimeoutSecondsIsDefault)
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
            settings.TimeoutSeconds is null);
    }
}
