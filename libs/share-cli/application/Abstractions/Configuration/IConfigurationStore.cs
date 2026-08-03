using SharedKernel;

namespace Share.Application.Abstractions.Configuration;

/// <summary>
/// Read/write access to the CLI's own configuration file — the source of truth for how
/// the CLI reaches the API. Where that file lives and what format it is in are
/// Infrastructure concerns.
/// </summary>
public interface IConfigurationStore
{
    /// <summary>
    /// Absolute path of the configuration file, whether or not it exists yet.
    /// </summary>
    string Location { get; }

    /// <summary>
    /// Whether the file is present. A missing file is not an error — it means every
    /// setting is defaulted.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Reads the settings the file sets. Unset settings come back <see langword="null"/>;
    /// a missing file yields all-null rather than a failure.
    /// </summary>
    Task<Result<ShareApiSettings>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the settings, creating the file and its directory if needed. Settings left
    /// <see langword="null"/> are removed from the file so they fall back to defaults.
    /// Any unrelated content in the file is preserved.
    /// </summary>
    Task<Result> SaveAsync(ShareApiSettings settings, CancellationToken cancellationToken = default);
}
