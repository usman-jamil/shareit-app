using Share.Domain.Updates;

namespace Share.Application.Updates.Apply;

/// <summary>
/// The updater has been started. Nothing has been downloaded or replaced yet — that
/// happens in the other process, once this one exits.
/// </summary>
/// <param name="CurrentVersion">The version still installed at this moment.</param>
/// <param name="TargetVersion">The release the updater will install.</param>
/// <param name="TagName">Its tag.</param>
/// <param name="TargetExecutablePath">The binary the updater will replace.</param>
/// <param name="UpdaterProcessId">The updater's process identifier.</param>
public sealed record ApplyUpdateResponse(
    SemanticVersion CurrentVersion,
    SemanticVersion TargetVersion,
    string TagName,
    string TargetExecutablePath,
    int UpdaterProcessId);
