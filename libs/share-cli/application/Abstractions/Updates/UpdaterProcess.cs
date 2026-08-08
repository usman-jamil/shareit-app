namespace Share.Application.Abstractions.Updates;

/// <summary>
/// The second instance of the CLI, started to perform the update.
/// </summary>
/// <param name="ProcessId">Its process identifier, reported so the user can find it.</param>
/// <param name="ExecutablePath">The temporary clone it is running from.</param>
public sealed record UpdaterProcess(int ProcessId, string ExecutablePath);
