namespace Share.Infrastructure.Updates;

/// <summary>
/// The temporary directory <c>share update</c> works in, and the sweeping of what earlier
/// runs left there.
/// </summary>
/// <remarks>
/// One thing an update genuinely cannot clean up after itself: the updater is running from
/// a copy of the CLI, and no process can delete the file it is executing. So the copy is
/// left behind and removed by whichever run comes next.
/// </remarks>
internal static class UpdateWorkspace
{
    /// <summary>
    /// How long a leftover directory is kept before a later run removes it. Comfortably
    /// longer than any update takes, so a run in progress is never swept out from under
    /// itself.
    /// </summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(1);

    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "share-cli-update");

    /// <summary>
    /// Creates a fresh working directory under <see cref="Root"/>.
    /// </summary>
    public static string CreateDirectory(string prefix)
    {
        string path = Path.Combine(Root, $"{prefix}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>
    /// Deletes <paramref name="path"/>. Best effort by design — every caller is on a path
    /// where failing to tidy up is not a reason to report failure.
    /// </summary>
    public static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The updater's own clone lands here on Windows, where a running executable
            // cannot be deleted. The next run sweeps it.
        }
    }

    /// <summary>
    /// Removes working directories older than <see cref="StaleAfter"/>.
    /// </summary>
    public static void Sweep()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        DateTime cutoff = DateTime.UtcNow - StaleAfter;

        try
        {
            foreach (string directory in Directory.EnumerateDirectories(Root))
            {
                if (Directory.GetCreationTimeUtc(directory) < cutoff)
                {
                    TryDelete(directory);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Sweeping is housekeeping, not part of the update.
        }
    }
}
