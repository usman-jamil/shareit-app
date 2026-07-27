namespace Share.Application.Abstractions.Api;

/// <summary>
/// The owner of a share, as the API reports it.
/// </summary>
public sealed record UserDetails(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);
