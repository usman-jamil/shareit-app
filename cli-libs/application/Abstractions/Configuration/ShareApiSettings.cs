namespace Share.Application.Abstractions.Configuration;

/// <summary>
/// The API settings as the configuration file holds them. <see langword="null"/> means
/// "not set in the file", which is distinct from "set to the default value" — only the
/// former falls back if the default ever changes.
/// </summary>
/// <param name="BaseUrl">Root address of the Share API.</param>
/// <param name="TimeoutSeconds">Per-request timeout in seconds.</param>
/// <param name="ApiKey">
/// Secret sent as the <c>X-Api-Key</c> header. Never put this on a response, in a log or in
/// an error message — it leaves the store only to be written straight back to the file.
/// </param>
/// <param name="UserId">
/// The user new shares are created for. Configured once rather than passed on every
/// command; <c>share create --user-id</c> overrides it for a single run.
/// </param>
public sealed record ShareApiSettings(Uri? BaseUrl, int? TimeoutSeconds, string? ApiKey, Guid? UserId)
{
    public static ShareApiSettings Empty { get; } = new(null, null, null, null);
}
