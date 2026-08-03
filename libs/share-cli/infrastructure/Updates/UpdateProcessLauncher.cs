using System.ComponentModel;
using System.Diagnostics;
using Share.Application.Abstractions.Updates;
using Share.Domain.Updates;
using SharedKernel;

namespace Share.Infrastructure.Updates;

/// <summary>
/// Clones the running executable into a temporary directory and starts it there.
/// </summary>
/// <remarks>
/// The clone is a straight file copy, which is only sound because a released CLI is a
/// self-contained single file — <c>IApplicationEnvironment.IsReleaseBuild</c> is what makes
/// sure of that before this is ever reached. <c>appsettings.json</c> is copied alongside it
/// when there is one, so the clone logs the way the original does.
/// </remarks>
internal sealed class UpdateProcessLauncher(IApplicationEnvironment environment)
    : IUpdateProcessLauncher
{
    private const string SettingsFileName = "appsettings.json";

    public Result<UpdaterProcess> Start(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (environment.ExecutablePath is not { } source)
        {
            return Result.Failure<UpdaterProcess>(UpdateErrors.ExecutablePathUnknown());
        }

        // Whatever earlier runs could not delete — an updater cannot remove the file it is
        // executing — goes now, while nothing is holding it.
        UpdateWorkspace.Sweep();

        string? directory = null;

        try
        {
            directory = UpdateWorkspace.CreateDirectory("updater");

            string clone = Path.Combine(directory, Path.GetFileName(source));

            File.Copy(source, clone);
            CopySettingsBeside(source, directory);
            MakeExecutable(clone);

            var startInfo = new ProcessStartInfo(clone)
            {
                // Not redirected: the clone writes to this process's console, so the user
                // sees how the update went after this process has gone.
                UseShellExecute = false
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);

            return process is null
                ? Failed(directory, "the operating system did not start it")
                : Result.Success(new UpdaterProcess(process.Id, clone));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return Failed(directory, exception.Message);
        }
    }

    public async Task<Result> WaitForExitAsync(
        int processId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (processId <= 0)
        {
            return Result.Success();
        }

        Process process;

        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            // Already gone. The common case, in fact: the process that started the updater
            // was on its way out as it did so.
            return Result.Success();
        }

        using (process)
        {
            try
            {
                using var deadline = new CancellationTokenSource(timeout);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    deadline.Token);

                await process.WaitForExitAsync(linked.Token);

                return Result.Success();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Result.Failure(UpdateErrors.CallerStillRunning(processId));
            }
        }
    }

    private static Result<UpdaterProcess> Failed(string? directory, string reason)
    {
        UpdateWorkspace.TryDelete(directory);

        return Result.Failure<UpdaterProcess>(UpdateErrors.LaunchFailed(reason));
    }

    /// <summary>
    /// <see cref="File.Copy(string, string)"/> does not carry the executable bit, so the
    /// clone would not run without this.
    /// </summary>
    private static void MakeExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void CopySettingsBeside(string executablePath, string directory)
    {
        if (Path.GetDirectoryName(executablePath) is not { Length: > 0 } source)
        {
            return;
        }

        string settings = Path.Combine(source, SettingsFileName);

        if (File.Exists(settings))
        {
            File.Copy(settings, Path.Combine(directory, SettingsFileName), overwrite: true);
        }
    }
}
