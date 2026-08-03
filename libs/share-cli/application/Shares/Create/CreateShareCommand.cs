using Share.Application.Abstractions.Messaging;

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
public sealed record CreateShareCommand(string DirectoryPath, Guid? OwnerUserId, int? TtlMinutes)
    : ICommand<CreateShareResponse>;
