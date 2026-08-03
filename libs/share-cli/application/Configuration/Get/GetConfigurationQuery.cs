using Share.Application.Abstractions.Messaging;

namespace Share.Application.Configuration.Get;

/// <summary>
/// Reads the configuration file and reports the effective settings.
/// </summary>
public sealed record GetConfigurationQuery : IQuery<ConfigurationResponse>;
