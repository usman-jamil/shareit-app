using Share.Application.Abstractions.Messaging;
using Share.Domain.Updates;

namespace Share.Application.Updates.Install;

/// <summary>
/// The second half of <c>share update</c>, run by the temporary clone: wait for the
/// original to exit, fetch the release, and put it in the original's place.
/// </summary>
/// <param name="Version">
/// The exact release to install. Already resolved by the process that started this one, so
/// "latest" is never re-evaluated here.
/// </param>
/// <param name="TargetExecutablePath">The binary to replace.</param>
/// <param name="CallerProcessId">
/// The process that started this one. Zero means there is nothing to wait for, which is
/// what running the command by hand amounts to.
/// </param>
public sealed record InstallUpdateCommand(
    SemanticVersion Version,
    string TargetExecutablePath,
    int CallerProcessId)
    : ICommand<InstallUpdateResponse>;
