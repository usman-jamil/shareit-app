namespace Share.Application.Abstractions.Configuration;

/// <summary>
/// One workspace as a listing needs it: enough to tell the workspaces apart without
/// switching to any of them.
/// </summary>
/// <param name="Name">The workspace's name, in the casing the file holds it.</param>
/// <param name="BaseUrl">
/// The server the workspace points at, exactly as the file spells it, or
/// <see langword="null"/> when it sets none. Left unvalidated on purpose: listing is how a
/// user diagnoses a bad file, so one malformed URL must not stop the rest being shown.
/// </param>
/// <remarks>
/// Carries no API key, and must not grow one. This is the one place that reads across every
/// workspace at once, so a secret on it would be a secret read for workspaces the user is
/// not even using.
/// </remarks>
public sealed record WorkspaceSummary(string Name, string? BaseUrl);
