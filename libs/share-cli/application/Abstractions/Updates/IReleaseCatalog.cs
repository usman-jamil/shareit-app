using Share.Domain.Updates;
using SharedKernel;

namespace Share.Application.Abstractions.Updates;

/// <summary>
/// The published releases of the CLI. Which host serves them and how they are tagged are
/// Infrastructure concerns.
/// </summary>
public interface IReleaseCatalog
{
    /// <summary>
    /// The newest stable release. Prereleases are never returned here — moving onto one is
    /// something the user asks for by name through <see cref="GetAsync"/>.
    /// </summary>
    Task<Result<ReleaseInfo>> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The release for an exact version, prerelease or not. Fails with
    /// <see cref="UpdateErrors.ReleaseNotFound"/> when there is no such release.
    /// </summary>
    Task<Result<ReleaseInfo>> GetAsync(
        SemanticVersion version,
        CancellationToken cancellationToken = default);
}
