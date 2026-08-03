using Share.Application.Abstractions.Messaging;
using Share.Domain.Updates;

namespace Share.Application.Updates.Apply;

/// <summary>
/// Hands the update over to a second instance of the CLI and returns. The caller is
/// expected to exit promptly afterwards — the updater is waiting for it to do so.
/// </summary>
/// <param name="RequestedVersion">
/// The release to install. <see langword="null"/> means the newest stable one. A version
/// below the one running is a downgrade and is carried out as asked.
/// </param>
public sealed record ApplyUpdateCommand(SemanticVersion? RequestedVersion)
    : ICommand<ApplyUpdateResponse>;
