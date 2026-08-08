using Share.Domain.Updates;

namespace Share.Application.Abstractions.Updates;

/// <summary>
/// What the running CLI knows about itself: which version it is, where its binary is, and
/// which release archive would replace it.
/// </summary>
/// <remarks>
/// All of it is reachable from <c>System.Environment</c> and <c>System.Reflection</c>, but
/// none of it is decidable in a unit test — which is exactly why the update use cases take
/// it as a dependency instead of reading it.
/// </remarks>
public interface IApplicationEnvironment
{
    /// <summary>
    /// The installed version, or <see langword="null"/> when this build does not carry one
    /// that parses — a local <c>dotnet run</c>, for instance.
    /// </summary>
    SemanticVersion? CurrentVersion { get; }

    /// <summary>
    /// Absolute path of the executable being run. <see langword="null"/> only in hosts that
    /// do not have one.
    /// </summary>
    string? ExecutablePath { get; }

    /// <summary>
    /// The runtime identifier whose release archive matches this machine, e.g.
    /// <c>osx-arm64</c>. <see langword="null"/> on a platform no release is published for.
    /// </summary>
    string? RuntimeIdentifier { get; }

    /// <summary>
    /// Human-readable platform, used to explain a <see langword="null"/>
    /// <see cref="RuntimeIdentifier"/>.
    /// </summary>
    string PlatformDescription { get; }

    /// <summary>
    /// This process's own identifier, handed to the updater so it knows what to wait for.
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// Whether the executable is the self-contained single file a release archive ships.
    /// A layout that keeps its assemblies on disk beside the host — a development build, or
    /// an install from source — cannot be updated by swapping one file, so it is refused
    /// rather than half-replaced.
    /// </summary>
    bool IsReleaseBuild { get; }
}
