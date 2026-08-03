using Share.Application.Abstractions.Updates;
using Share.Domain.Updates;

namespace Share.Infrastructure.UnitTests.Updates;

/// <summary>
/// A fixed answer to "what am I running on". Hand-written rather than mocked: every member
/// is a plain value, and pinning the runtime identifier is what makes these tests behave
/// the same on every machine that runs them.
/// </summary>
internal sealed class StubApplicationEnvironment(
    string? runtimeIdentifier = "linux-x64",
    string? currentVersion = "1.0.0")
    : IApplicationEnvironment
{
    public SemanticVersion? CurrentVersion { get; } =
        currentVersion is null ? null : Parse(currentVersion);

    public string? ExecutablePath => "/usr/local/bin/share";

    public string? RuntimeIdentifier { get; } = runtimeIdentifier;

    public string PlatformDescription => "Test OS (Arm64)";

    public int ProcessId => 100;

    public bool IsReleaseBuild => true;

    private static SemanticVersion Parse(string text) =>
        SemanticVersion.TryParse(text, out SemanticVersion? version)
            ? version!
            : throw new ArgumentException($"'{text}' is not a version.", nameof(text));
}
