namespace Share.Application.Abstractions.Updates;

/// <summary>
/// A release that has been downloaded, verified and unpacked, and is ready to be moved
/// into place.
/// </summary>
/// <param name="ExecutablePath">The new binary, sitting under <paramref name="Directory"/>.</param>
/// <param name="Directory">
/// Everything the staging step created. Handed back to
/// <see cref="IUpdatePackageInstaller.Discard"/> so nothing is left in the temp directory.
/// </param>
public sealed record StagedUpdate(string ExecutablePath, string Directory);
