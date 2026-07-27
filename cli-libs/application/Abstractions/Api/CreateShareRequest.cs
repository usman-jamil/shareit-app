namespace Share.Application.Abstractions.Api;

/// <summary>
/// The manifest of files the CLI intends to upload as one share.
/// </summary>
/// <param name="OwnerUserId">The user the share belongs to.</param>
/// <param name="ConfiguredTtlMinutes">
/// How long the share should live. <see langword="null"/> lets the API apply its own
/// default, which is the normal case — only send a value when the user asked for one.
/// </param>
/// <param name="Files">
/// Every file to include, at least one. Paths are relative to the share root, so a
/// recursive directory upload is expressed as nested paths in this collection.
/// </param>
public sealed record CreateShareRequest(
    Guid OwnerUserId,
    int? ConfiguredTtlMinutes,
    IReadOnlyCollection<FileUploadRequest> Files);
