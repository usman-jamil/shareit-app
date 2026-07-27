namespace Share.Application.Abstractions.Configuration;

/// <summary>
/// The API settings as the configuration file holds them. <see langword="null"/> means
/// "not set in the file", which is distinct from "set to the default value" — only the
/// former falls back if the default ever changes.
/// </summary>
/// <param name="BaseUrl">Root address of the Share API.</param>
/// <param name="TimeoutSeconds">Per-request timeout in seconds.</param>
public sealed record ShareApiSettings(Uri? BaseUrl, int? TimeoutSeconds)
{
    public static ShareApiSettings Empty { get; } = new(null, null);
}
