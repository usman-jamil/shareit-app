using System.Globalization;

namespace Share.Domain.Updates;

/// <summary>
/// A release version, in the shape the release workflow accepts:
/// <c>MAJOR.MINOR.PATCH</c> with an optional <c>-prerelease</c> suffix.
/// </summary>
/// <remarks>
/// <see cref="Version"/> cannot carry a prerelease suffix and orders
/// <c>1.0.0-beta.1</c> the same as <c>1.0.0</c>, which is exactly the comparison an
/// updater must not get wrong. Precedence here follows semver 2.0: build metadata is
/// discarded, a prerelease sorts below the release it leads to, and prerelease
/// identifiers are compared field by field, numerically where both fields are numeric.
/// </remarks>
public sealed record SemanticVersion : IComparable<SemanticVersion>
{
    private SemanticVersion(int major, int minor, int patch, string? preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>
    /// The suffix after the first <c>-</c>, or <see langword="null"/> for a stable release.
    /// </summary>
    public string? PreRelease { get; }

    public bool IsPreRelease => PreRelease is not null;

    /// <summary>
    /// Parses <paramref name="text"/>, tolerating a leading <c>v</c> and discarding build
    /// metadata after <c>+</c>. Returns <see langword="false"/> for anything else, which
    /// includes the empty string and versions with fewer than three numeric parts.
    /// </summary>
    public static bool TryParse(string? text, out SemanticVersion? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        ReadOnlySpan<char> remaining = text.AsSpan().Trim();

        if (remaining.Length > 0 && (remaining[0] == 'v' || remaining[0] == 'V'))
        {
            remaining = remaining[1..];
        }

        // Build metadata is not part of precedence, so it is dropped rather than kept and
        // then ignored everywhere it would otherwise have to be.
        int build = remaining.IndexOf('+');

        if (build >= 0)
        {
            remaining = remaining[..build];
        }

        string? preRelease = null;
        int dash = remaining.IndexOf('-');

        if (dash >= 0)
        {
            ReadOnlySpan<char> suffix = remaining[(dash + 1)..];

            if (suffix.IsEmpty)
            {
                return false;
            }

            preRelease = suffix.ToString();
            remaining = remaining[..dash];
        }

        if (!TryParseCore(remaining, out int major, out int minor, out int patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, preRelease);

        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int comparison = Major.CompareTo(other.Major);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Minor.CompareTo(other.Minor);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Patch.CompareTo(other.Patch);

        return comparison != 0
            ? comparison
            : ComparePreRelease(PreRelease, other.PreRelease);
    }

    public override string ToString()
    {
        string core = string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Patch}");

        return PreRelease is null ? core : $"{core}-{PreRelease}";
    }

    public static bool operator <(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(SemanticVersion? left, SemanticVersion? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(SemanticVersion? left, SemanticVersion? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    private static bool TryParseCore(
        ReadOnlySpan<char> text,
        out int major,
        out int minor,
        out int patch)
    {
        major = 0;
        minor = 0;
        patch = 0;

        Span<Range> parts = stackalloc Range[4];

        // 4 rather than 3: a fourth part makes the split return 4 and the check below
        // reject it, instead of the extra part being silently folded into the third.
        if (text.Split(parts, '.') != 3)
        {
            return false;
        }

        return TryParseNumber(text[parts[0]], out major)
            && TryParseNumber(text[parts[1]], out minor)
            && TryParseNumber(text[parts[2]], out patch);
    }

    private static bool TryParseNumber(ReadOnlySpan<char> text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Semver precedence for the prerelease field: a version without one outranks the same
    /// version with one, and otherwise identifiers are compared left to right.
    /// </summary>
    private static int ComparePreRelease(string? left, string? right)
    {
        if (left is null)
        {
            return right is null ? 0 : 1;
        }

        if (right is null)
        {
            return -1;
        }

        string[] leftFields = left.Split('.');
        string[] rightFields = right.Split('.');

        int shared = Math.Min(leftFields.Length, rightFields.Length);

        for (int index = 0; index < shared; index++)
        {
            int comparison = CompareIdentifier(leftFields[index], rightFields[index]);

            if (comparison != 0)
            {
                return comparison;
            }
        }

        // Everything shared is equal, so the longer suffix is the higher precedence:
        // 1.0.0-beta.1 outranks 1.0.0-beta.
        return leftFields.Length.CompareTo(rightFields.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        bool leftIsNumeric = TryParseNumber(left, out int leftValue);
        bool rightIsNumeric = TryParseNumber(right, out int rightValue);

        return (leftIsNumeric, rightIsNumeric) switch
        {
            (true, true) => leftValue.CompareTo(rightValue),

            // Numeric identifiers always have lower precedence than alphanumeric ones.
            (true, false) => -1,
            (false, true) => 1,
            _ => string.CompareOrdinal(left, right)
        };
    }
}
