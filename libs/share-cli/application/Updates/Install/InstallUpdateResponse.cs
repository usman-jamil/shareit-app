using Share.Domain.Updates;

namespace Share.Application.Updates.Install;

/// <summary>
/// The binary at <paramref name="TargetExecutablePath"/> is now
/// <paramref name="Version"/>.
/// </summary>
/// <param name="Version">The release that was installed.</param>
/// <param name="TagName">Its tag.</param>
/// <param name="TargetExecutablePath">The binary that was replaced.</param>
public sealed record InstallUpdateResponse(
    SemanticVersion Version,
    string TagName,
    string TargetExecutablePath);
