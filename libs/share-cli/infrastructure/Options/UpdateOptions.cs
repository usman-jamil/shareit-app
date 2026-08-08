using Share.Domain.Updates;

namespace Share.Infrastructure.Options;

/// <summary>
/// Where <c>share update</c> looks for releases. Bound from the <c>Update</c> configuration
/// section, which exists so a fork or a test environment can be pointed elsewhere; the
/// defaults are the real repository and nothing has to be configured to use it.
/// </summary>
public sealed class UpdateOptions
{
    public const string SectionName = "Update";

    public Uri ApiBaseUrl { get; set; } = UpdateDefaults.ApiBaseUrl;

    public string RepositoryOwner { get; set; } = UpdateDefaults.RepositoryOwner;

    public string RepositoryName { get; set; } = UpdateDefaults.RepositoryName;

    /// <summary>
    /// Only tags starting with this are considered releases of the CLI.
    /// </summary>
    public string TagPrefix { get; set; } = UpdateDefaults.TagPrefix;

    /// <summary>
    /// Per-request timeout for the release listing. The archive download is not bound by
    /// it — see <c>UpdatePackageInstaller</c>.
    /// </summary>
    public int TimeoutSeconds { get; set; } = UpdateDefaults.TimeoutSeconds;
}
