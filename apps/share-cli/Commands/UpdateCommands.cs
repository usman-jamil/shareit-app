using System.Globalization;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Share.Application.Abstractions.Messaging;
using Share.Application.Updates.Apply;
using Share.Application.Updates.Check;
using Share.Application.Updates.Install;
using Share.Cli.Rendering;
using Share.Domain.Updates;
using SharedKernel;

namespace Share.Cli.Commands;

/// <summary>
/// Updating the CLI in place from its GitHub releases.
/// </summary>
/// <remarks>
/// A process cannot overwrite the file it is running from, so <c>share update</c> is two
/// commands: this one resolves the release and starts a copy of itself, and that copy runs
/// <see cref="UpdaterCommandLine.Verb"/> to do the download and the swap once this process
/// has exited.
/// </remarks>
public class UpdateCommands(IServiceProvider serviceProvider)
{
    /// <summary>
    /// Update to the latest release, or to a specific version.
    /// </summary>
    /// <param name="check">-c, Report what an update would do and exit without changing anything.</param>
    /// <param name="version">-v, Release to move to, e.g. 1.3.2. Lower than the installed version downgrades.</param>
    /// <param name="yes">-y, Do not ask for confirmation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Command("update")]
    public async Task<int> Update(
        bool check = false,
        string? version = null,
        bool yes = false,
        CancellationToken cancellationToken = default)
    {
        SemanticVersion? requested = null;

        if (version is not null && !SemanticVersion.TryParse(version, out requested))
        {
            await Console.Error.WriteLineAsync(UpdateErrors.InvalidVersion(version).Description);

            return 1;
        }

        using IServiceScope scope = serviceProvider.CreateScope();

        IQueryHandler<CheckForUpdateQuery, UpdateCheckResponse> checkHandler =
            scope.ServiceProvider
                .GetRequiredService<IQueryHandler<CheckForUpdateQuery, UpdateCheckResponse>>();

        Result<UpdateCheckResponse> lookup =
            await checkHandler.Handle(new CheckForUpdateQuery(requested), cancellationToken);

        if (lookup.IsFailure)
        {
            return Fail(lookup.Error);
        }

        UpdateCheckResponse status = lookup.Value;

        Write(status);

        if (check)
        {
            return 0;
        }

        // Re-running the installed version would download and swap in a byte-identical
        // binary. Reporting it costs nothing and is what the user meant to ask.
        if (status.Action == UpdateAction.UpToDate)
        {
            Console.WriteLine();
            Console.WriteLine($"Already on {status.CurrentVersion}. Nothing to do.");

            return 0;
        }

        int? declined = Confirm(status, yes);

        if (declined is { } exitCode)
        {
            return exitCode;
        }

        ICommandHandler<ApplyUpdateCommand, ApplyUpdateResponse> applyHandler =
            scope.ServiceProvider
                .GetRequiredService<ICommandHandler<ApplyUpdateCommand, ApplyUpdateResponse>>();

        // The version resolved above rather than what was typed, so the updater installs the
        // release that was just shown even if a newer one is published in between.
        Result<ApplyUpdateResponse> applied = await applyHandler.Handle(
            new ApplyUpdateCommand(status.TargetVersion),
            cancellationToken);

        if (applied.IsFailure)
        {
            return Fail(applied.Error);
        }

        Write(applied.Value);

        return 0;
    }

    /// <summary>
    /// Second stage of `share update`, run by a temporary copy of the CLI. Not meant to be
    /// invoked by hand.
    /// </summary>
    /// <param name="version">Release to install, e.g. 1.3.2.</param>
    /// <param name="target">Executable to replace.</param>
    /// <param name="callerProcessId">Process to wait for before replacing it. 0 waits for nothing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Command(UpdaterCommandLine.Verb)]
    [Hidden]
    public async Task<int> UpdateApply(
        string version,
        string target,
        int callerProcessId = 0,
        CancellationToken cancellationToken = default)
    {
        if (!SemanticVersion.TryParse(version, out SemanticVersion? parsed))
        {
            await Console.Error.WriteLineAsync(UpdateErrors.InvalidVersion(version).Description);

            return 1;
        }

        using IServiceScope scope = serviceProvider.CreateScope();

        ICommandHandler<InstallUpdateCommand, InstallUpdateResponse> handler =
            scope.ServiceProvider
                .GetRequiredService<ICommandHandler<InstallUpdateCommand, InstallUpdateResponse>>();

        Result<InstallUpdateResponse> result = await handler.Handle(
            new InstallUpdateCommand(parsed!, target, callerProcessId),
            cancellationToken);

        if (result.IsFailure)
        {
            return Fail(result.Error);
        }

        Console.WriteLine(
            $"Updated {result.Value.TargetExecutablePath} to {result.Value.Version}.");

        return 0;
    }

    /// <summary>
    /// Asks before replacing the binary. Returns <see langword="null"/> to go ahead, or the
    /// exit code to stop with: declining is a choice and succeeds, whereas not being able
    /// to ask at all is a failure the caller needs to see.
    /// </summary>
    private static int? Confirm(UpdateCheckResponse status, bool yes)
    {
        if (yes)
        {
            return null;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine(
                "Standard input is not a terminal, so the update cannot be confirmed. " +
                "Re-run with --yes.");

            return 1;
        }

        string verb = status.Action == UpdateAction.Downgrade ? "Downgrade" : "Update";

        Console.WriteLine();
        Console.Write($"{verb} to {status.TargetVersion}? [y/N] ");

        string answer = (Console.ReadLine() ?? string.Empty).Trim();

        if (answer.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            answer.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        Console.WriteLine("Cancelled.");

        return 0;
    }

    private static void Write(UpdateCheckResponse status)
    {
        Console.WriteLine($"Installed  {status.CurrentVersion}");
        Console.WriteLine(
            $"Latest     {status.TargetVersion}{(status.IsPreRelease ? "  (prerelease)" : string.Empty)}");
        Console.WriteLine($"Status     {Describe(status.Action)}");

        if (status.PublishedAt is { } published)
        {
            Console.WriteLine(
                $"Published  {published.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");
        }

        if (status.ReleaseUrl is { } url)
        {
            Console.WriteLine($"Notes      {url}");
        }
    }

    private static void Write(ApplyUpdateResponse applied)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"Updating {applied.TargetExecutablePath} to {applied.TargetVersion} in process " +
            applied.UpdaterProcessId.ToString(CultureInfo.InvariantCulture) + ".");

        // Said plainly because the shell prompt comes back first and the result lands after
        // it, which otherwise reads like the command did nothing.
        Console.WriteLine(
            "This process is exiting so its binary can be replaced; the result follows.");
    }

    private static string Describe(UpdateAction action) => action switch
    {
        UpdateAction.Upgrade => "an update is available",
        UpdateAction.Downgrade => "older than what is installed",
        _ => "up to date"
    };

    private static int Fail(Error error) => ConsoleOutput.Fail(error);
}
