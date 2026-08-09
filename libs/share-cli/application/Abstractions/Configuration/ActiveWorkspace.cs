namespace Share.Application.Abstractions.Configuration;

/// <summary>
/// The workspace the CLI is currently pointed at, together with what that workspace sets.
/// Read as one unit so the name and the values can never disagree about which server a
/// command is about to talk to.
/// </summary>
/// <param name="Name">
/// Name of the active workspace — the root-level section the settings came from.
/// </param>
/// <param name="Settings">
/// What that section sets. All-null when the section is absent, which is not an error.
/// </param>
public sealed record ActiveWorkspace(string Name, ShareApiSettings Settings);
