namespace Share.Domain.Updates;

/// <summary>
/// Reads the <c>SHA256SUMS.txt</c> asset a release publishes: one
/// <c>&lt;hash&gt;&#160;&#160;&lt;file name&gt;</c> line per archive, as written by
/// <c>sha256sum</c>.
/// </summary>
public static class Sha256Sums
{
    private const int HashLength = 64;

    /// <summary>
    /// Finds the expected hash for <paramref name="fileName"/>. A line whose name is
    /// prefixed with <c>*</c> — how <c>sha256sum</c> marks binary mode — matches the same
    /// file. Returns <see langword="false"/> when the file is not listed, which is treated
    /// as an unverifiable download rather than as a passing one.
    /// </summary>
    public static bool TryFind(string? content, string fileName, out string hash)
    {
        hash = string.Empty;

        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        foreach (string line in content.Split('\n', StringSplitOptions.TrimEntries))
        {
            int separator = line.IndexOf(' ', StringComparison.Ordinal);

            if (separator != HashLength)
            {
                continue;
            }

            string name = line[(separator + 1)..].TrimStart(' ', '*');

            if (!string.Equals(name, fileName, StringComparison.Ordinal))
            {
                continue;
            }

            hash = line[..separator];

            return true;
        }

        return false;
    }
}
