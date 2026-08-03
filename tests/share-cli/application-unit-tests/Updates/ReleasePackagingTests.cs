using Share.Domain.Updates;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Updates;

/// <summary>
/// Pins the names the release workflow writes. If these change, the workflow changed and
/// the CLI would be downloading assets that are not there.
/// </summary>
public sealed class ReleasePackagingTests
{
    [Theory]
    [InlineData("linux-x64", "share-1.2.3-linux-x64.tar.gz")]
    [InlineData("linux-arm64", "share-1.2.3-linux-arm64.tar.gz")]
    [InlineData("osx-arm64", "share-1.2.3-osx-arm64.tar.gz")]
    [InlineData("win-x64", "share-1.2.3-win-x64.zip")]
    [InlineData("win-arm64", "share-1.2.3-win-arm64.zip")]
    public void ArchiveName_Should_MatchWhatTheWorkflowPublishes(
        string runtimeIdentifier,
        string expected)
    {
        SemanticVersion.TryParse("1.2.3", out SemanticVersion? version).ShouldBeTrue();

        ReleasePackaging.ArchiveName(version!, runtimeIdentifier).ShouldBe(expected);
    }

    [Fact]
    public void ArchiveName_Should_CarryThePreReleaseSuffix()
    {
        SemanticVersion.TryParse("1.2.3-beta.1", out SemanticVersion? version).ShouldBeTrue();

        ReleasePackaging
            .ArchiveName(version!, "osx-arm64")
            .ShouldBe("share-1.2.3-beta.1-osx-arm64.tar.gz");
    }

    [Theory]
    [InlineData("win-x64", "share.exe")]
    [InlineData("linux-x64", "share")]
    [InlineData("osx-arm64", "share")]
    public void ExecutableName_Should_BeExeOnWindowsOnly(string runtimeIdentifier, string expected) =>
        ReleasePackaging.ExecutableName(runtimeIdentifier).ShouldBe(expected);
}
