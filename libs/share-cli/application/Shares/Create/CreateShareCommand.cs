using Share.Application.Abstractions.Messaging;
using Share.Application.Abstractions.Progress;

namespace Share.Application.Shares.Create;

/// <summary>
/// Shares the entire contents of a local directory: every file beneath it becomes one
/// file in the share, keyed by its path relative to the directory.
/// </summary>
/// <param name="DirectoryPath">
/// The share root. Everything under it is included, recursively.
/// </param>
/// <param name="OwnerUserId">
/// Who the share belongs to. <see langword="null"/> falls back to the user id in the
/// configuration file.
/// </param>
/// <param name="TtlMinutes">
/// How long the share should live. <see langword="null"/> lets the API apply its own
/// default, which is the normal case.
/// </param>
/// <param name="Progress">
/// Where to report upload progress, or <see langword="null"/> to report none. Carried on
/// the command rather than injected because a display belongs to one invocation: the caller
/// that renders it is the caller that owns it, and nothing else in the process should be
/// able to write to it.
/// </param>
public sealed record CreateShareCommand(
    string DirectoryPath,
    Guid? OwnerUserId,
    int? TtlMinutes,
    IUploadProgressReporter? Progress = null)
    : ICommand<CreateShareResponse>;
