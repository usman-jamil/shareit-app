using SharedKernel;

namespace Share.Application.Abstractions.Updates;

/// <summary>
/// Runs a second instance of the CLI — the one that does the update — and waits on the
/// first one.
/// </summary>
/// <remarks>
/// A process cannot reliably overwrite the file it is running from: Windows holds an
/// executable open for the life of the process, and on Unix an in-place write to a running
/// image is refused. So <c>share update</c> starts a copy of itself somewhere else and
/// exits; the copy waits for that exit and only then downloads and swaps the binary.
/// </remarks>
public interface IUpdateProcessLauncher
{
    /// <summary>
    /// Clones the running executable into a temporary directory and starts it with
    /// <paramref name="arguments"/>. The clone inherits this process's console, so what it
    /// prints reaches the user after this process has gone.
    /// </summary>
    Result<UpdaterProcess> Start(IReadOnlyList<string> arguments);

    /// <summary>
    /// Waits for a process to exit. A process that has already gone — or an identifier of
    /// zero, meaning "nothing to wait for" — succeeds immediately; one still running when
    /// <paramref name="timeout"/> elapses fails with
    /// <see cref="Domain.Updates.UpdateErrors.CallerStillRunning"/>.
    /// </summary>
    Task<Result> WaitForExitAsync(
        int processId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
