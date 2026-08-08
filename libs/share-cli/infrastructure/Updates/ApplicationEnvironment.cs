using System.Reflection;
using System.Runtime.InteropServices;
using Share.Application.Abstractions.Updates;
using Share.Domain.Updates;

namespace Share.Infrastructure.Updates;

/// <summary>
/// Answers the update use cases' questions about the running process from
/// <c>System.Environment</c> and the entry assembly.
/// </summary>
internal sealed class ApplicationEnvironment : IApplicationEnvironment
{
    public ApplicationEnvironment()
    {
        ExecutablePath = Environment.ProcessPath;
        CurrentVersion = ResolveCurrentVersion();
        RuntimeIdentifier = ResolveRuntimeIdentifier();
        PlatformDescription =
            $"{RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.ProcessArchitecture})";
    }

    public SemanticVersion? CurrentVersion { get; }

    public string? ExecutablePath { get; }

    public string? RuntimeIdentifier { get; }

    public string PlatformDescription { get; }

    public int ProcessId => Environment.ProcessId;

    /// <summary>
    /// A published single-file build carries its assemblies inside the executable, so the
    /// managed entry assembly is not sitting next to it. A development build — anything
    /// under <c>dotnet run</c> or a plain <c>dotnet build</c> — has <c>share.dll</c> right
    /// there, and that is the tell.
    /// </summary>
    public bool IsReleaseBuild =>
        ExecutablePath is not null &&
        !File.Exists(Path.ChangeExtension(ExecutablePath, ".dll"));

    /// <summary>
    /// Read the same way ConsoleAppFramework reads it for <c>share --version</c>: the
    /// informational version with the <c>+sha</c> source revision dropped. The two must
    /// agree, or an update would compare against a version the user was never shown.
    /// </summary>
    private static SemanticVersion? ResolveCurrentVersion()
    {
        string? informational = Assembly
            .GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return SemanticVersion.TryParse(informational, out SemanticVersion? version)
            ? version
            : null;
    }

    /// <summary>
    /// Composed rather than taken from <see cref="RuntimeInformation.RuntimeIdentifier"/>:
    /// the result has to be one of the six the release workflow publishes, and anything
    /// else has to come back as "no archive for this platform" rather than as a download
    /// that 404s.
    /// </summary>
    /// <remarks>
    /// Keyed off the <em>process</em> architecture, not the OS architecture, so an x64
    /// build running under Rosetta on Apple silicon stays x64 instead of quietly changing
    /// flavour on update.
    /// </remarks>
    private static string? ResolveRuntimeIdentifier()
    {
        string? platform = ResolvePlatform();

        string? architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };

        return platform is null || architecture is null
            ? null
            : $"{platform}-{architecture}";
    }

    private static string? ResolvePlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        return OperatingSystem.IsLinux() ? "linux" : null;
    }
}
