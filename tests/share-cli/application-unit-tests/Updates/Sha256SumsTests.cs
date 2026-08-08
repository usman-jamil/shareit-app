using Share.Domain.Updates;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Updates;

public sealed class Sha256SumsTests
{
    private const string ArchiveHash =
        "a94a8fe5ccb19ba61c4c0873d391e987982fbbd3a94a8fe5ccb19ba61c4c0873";

    private const string OtherHash =
        "0000000000000000000000000000000000000000000000000000000000000000";

    // Exactly what `sha256sum` writes: hash, two spaces, name — with CRLF thrown in, since
    // the release collects lines produced on more than one machine.
    private const string Content =
        $"{OtherHash}  share-1.2.3-linux-x64.tar.gz\r\n" +
        $"{ArchiveHash}  share-1.2.3-osx-arm64.tar.gz\n";

    [Fact]
    public void TryFind_Should_ReturnTheHashForTheNamedFile()
    {
        Sha256Sums.TryFind(Content, "share-1.2.3-osx-arm64.tar.gz", out string hash).ShouldBeTrue();

        hash.ShouldBe(ArchiveHash);
    }

    [Fact]
    public void TryFind_Should_MatchALineWrittenInBinaryMode()
    {
        string content = $"{ArchiveHash} *share-1.2.3-win-x64.zip";

        Sha256Sums.TryFind(content, "share-1.2.3-win-x64.zip", out string hash).ShouldBeTrue();

        hash.ShouldBe(ArchiveHash);
    }

    [Theory]
    [InlineData("share-9.9.9-osx-arm64.tar.gz")]
    [InlineData("SHARE-1.2.3-OSX-ARM64.TAR.GZ")]
    public void TryFind_Should_Fail_WhenTheFileIsNotListed(string fileName)
    {
        // An unlisted file is an unverifiable download, so it must not come back as found.
        // The comparison is ordinal because these names are matched byte for byte upstream.
        Sha256Sums.TryFind(Content, fileName, out string hash).ShouldBeFalse();

        hash.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a checksums file")]
    [InlineData("tooshort  share-1.2.3-osx-arm64.tar.gz")]
    public void TryFind_Should_Fail_OnContentThatIsNotChecksums(string? content) =>
        Sha256Sums.TryFind(content, "share-1.2.3-osx-arm64.tar.gz", out _).ShouldBeFalse();
}
