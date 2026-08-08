using Share.Domain.Updates;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Updates;

/// <summary>
/// The comparisons an updater must not get wrong: what counts as a version, and which of
/// two is newer.
/// </summary>
public sealed class SemanticVersionTests
{
    private static readonly string[] Unordered =
        ["1.2.3", "1.0.0", "1.2.3-rc.1", "2.0.0", "1.2.3-beta.2"];

    [Theory]
    [InlineData("1.2.3", 1, 2, 3, null)]
    [InlineData("v1.2.3", 1, 2, 3, null)]
    [InlineData("V0.0.1", 0, 0, 1, null)]
    [InlineData("1.2.3-beta.1", 1, 2, 3, "beta.1")]
    [InlineData(" 10.20.30 ", 10, 20, 30, null)]

    // Build metadata is not part of precedence, so it is dropped. This is the shape the
    // SDK produces for AssemblyInformationalVersion when SourceLink is on.
    [InlineData("1.2.3+abcdef0", 1, 2, 3, null)]
    [InlineData("1.2.3-rc.1+abcdef0", 1, 2, 3, "rc.1")]
    public void TryParse_Should_Accept(
        string text,
        int major,
        int minor,
        int patch,
        string? preRelease)
    {
        SemanticVersion.TryParse(text, out SemanticVersion? version).ShouldBeTrue();

        version!.Major.ShouldBe(major);
        version.Minor.ShouldBe(minor);
        version.Patch.ShouldBe(patch);
        version.PreRelease.ShouldBe(preRelease);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.x")]
    [InlineData("-1.2.3")]
    [InlineData("1.2.3-")]
    [InlineData("latest")]
    public void TryParse_Should_Reject(string? text)
    {
        SemanticVersion.TryParse(text, out SemanticVersion? version).ShouldBeFalse();
        version.ShouldBeNull();
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-beta.1", "1.2.3-beta.1")]
    [InlineData("v1.2.3+sha", "1.2.3")]
    public void ToString_Should_RoundTripWhatPrecedenceKeeps(string text, string expected)
    {
        SemanticVersion.TryParse(text, out SemanticVersion? version).ShouldBeTrue();

        version!.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData("2.0.0", "1.9.9")]
    [InlineData("1.3.0", "1.2.9")]
    [InlineData("1.2.4", "1.2.3")]

    // A release outranks the prerelease that led to it.
    [InlineData("1.2.3", "1.2.3-rc.1")]

    // Numeric identifiers compare numerically, not as text — 10 is after 9.
    [InlineData("1.2.3-beta.10", "1.2.3-beta.9")]

    // Alphanumeric identifiers outrank numeric ones.
    [InlineData("1.2.3-beta", "1.2.3-1")]

    // A longer prerelease outranks the prefix it extends.
    [InlineData("1.2.3-beta.1", "1.2.3-beta")]
    [InlineData("1.2.3-rc.1", "1.2.3-beta.11")]
    public void CompareTo_Should_RankTheFirstHigher(string higher, string lower)
    {
        SemanticVersion left = Parse(higher);
        SemanticVersion right = Parse(lower);

        left.CompareTo(right).ShouldBeGreaterThan(0);
        right.CompareTo(left).ShouldBeLessThan(0);
        (left > right).ShouldBeTrue();
        (right < left).ShouldBeTrue();
    }

    [Fact]
    public void CompareTo_Should_TreatEqualVersionsAsEqual()
    {
        SemanticVersion left = Parse("1.2.3-beta.1");
        SemanticVersion right = Parse("1.2.3-beta.1");

        left.CompareTo(right).ShouldBe(0);
        left.ShouldBe(right);
        (left >= right).ShouldBeTrue();
        (left <= right).ShouldBeTrue();
    }

    [Fact]
    public void Ordering_Should_PutTheNewestLast()
    {
        // The default comparer is what the release catalogue sorts by, so this is the
        // behaviour that decides which release "latest" means.
        string[] ordered =
        [
            .. Unordered.Select(Parse).Order().Select(version => version.ToString())
        ];

        ordered.ShouldBe(["1.0.0", "1.2.3-beta.2", "1.2.3-rc.1", "1.2.3", "2.0.0"]);
    }

    private static SemanticVersion Parse(string text)
    {
        SemanticVersion.TryParse(text, out SemanticVersion? version).ShouldBeTrue();

        return version!;
    }
}
