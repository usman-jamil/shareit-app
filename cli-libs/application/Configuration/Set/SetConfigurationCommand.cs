using Share.Application.Abstractions.Messaging;

namespace Share.Application.Configuration.Set;

/// <summary>
/// Updates the configuration file. Settings left <see langword="null"/> keep whatever the
/// file already holds, so a single setting can be changed without restating the rest.
/// </summary>
public sealed record SetConfigurationCommand(Uri? BaseUrl, int? TimeoutSeconds)
    : ICommand<ConfigurationResponse>;
