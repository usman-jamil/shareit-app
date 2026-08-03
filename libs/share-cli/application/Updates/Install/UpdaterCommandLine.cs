namespace Share.Application.Updates.Install;

/// <summary>
/// The command line the first instance of the CLI uses to start the second one.
/// </summary>
/// <remarks>
/// Both ends of that handover live in different projects — <c>ApplyUpdateCommandHandler</c>
/// composes the arguments, <c>Share.Cli</c>'s <c>UpdateCommands</c> declares the command
/// that receives them — so the names they have to agree on are stated once, here. The
/// option names are derived by ConsoleAppFramework from the receiving method's parameter
/// names; keep them in step.
/// </remarks>
public static class UpdaterCommandLine
{
    /// <summary>
    /// The hidden command the updater runs. Hidden because it is an implementation detail
    /// of <c>share update</c>, not something to invoke by hand.
    /// </summary>
    public const string Verb = "update-apply";

    /// <summary>The release to install, e.g. <c>1.3.2</c>.</summary>
    public const string VersionOption = "--version";

    /// <summary>The executable to replace.</summary>
    public const string TargetOption = "--target";

    /// <summary>The process to wait for before replacing it.</summary>
    public const string CallerProcessIdOption = "--caller-process-id";
}
