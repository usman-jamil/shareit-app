using Share.Application.Abstractions.Messaging;

namespace Share.Application.Configuration.Set;

/// <summary>
/// Updates the configuration file. Settings left <see langword="null"/> keep whatever the
/// file already holds, so a single setting can be changed without restating the rest.
/// </summary>
/// <param name="BaseUrl">Root address of the Share API.</param>
/// <param name="TimeoutSeconds">Per-request timeout in seconds.</param>
/// <param name="ApiKey">
/// Secret sent as the <c>X-Api-Key</c> header. Only ever written to the configuration file —
/// it is never echoed back in a response, a log line or an error message.
/// </param>
/// <param name="UserId">The user new shares are created for.</param>
public sealed record SetConfigurationCommand(
    Uri? BaseUrl,
    int? TimeoutSeconds,
    string? ApiKey,
    Guid? UserId)
    : ICommand<ConfigurationResponse>;
