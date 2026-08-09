using System.Formats.Tar;
using System.IO.Compression;
using Share.Infrastructure.Updates;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Infrastructure.UnitTests.Updates;

/// <summary>
/// Pins the two things the unpacking step has to survive: a destination reached through a
/// symbolic link, and bytes that are not an archive at all.
/// </summary>
/// <remarks>
/// The symlink case is the one that shipped broken. macOS puts the temporary directory under
/// <c>/var</c>, which is a link to <c>/private/var</c>, and the extraction of an entry named
/// <c>share</c> died there with an <see cref="ArgumentOutOfRangeException"/> — a crash rather
/// than a failure result, in the middle of replacing the user's binary. The archives are
/// built with the <c>./</c> root entry that <c>tar --directory … .</c> writes in the release
/// workflow, so what is extracted here is the shape a real release actually has.
/// </remarks>
public sealed class ArchiveExtractorTests : IDisposable
{
    private const string RuntimeIdentifier = "linux-x64";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"share-cli-extractor-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractAsync_Should_UnpackTheBinary_WhenTheDestinationIsReachedThroughASymlink()
    {
        Assert.SkipWhen(
            OperatingSystem.IsWindows(),
            "Creating a symbolic link on Windows needs privileges the test host may not have.");

        string physical = Path.Combine(_root, "physical");
        string link = Path.Combine(_root, "link");

        Directory.CreateDirectory(physical);
        Directory.CreateSymbolicLink(link, physical);

        string archive = await GivenArchiveAsync("the 1.2.0 binary");
        string destination = Path.Combine(link, "extracted");

        Result result = await ArchiveExtractor.ExtractAsync(
            archive,
            destination,
            RuntimeIdentifier,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : null);

        // Asserted against the real directory rather than the link, so the test would still
        // notice if the extraction landed somewhere else entirely.
        string unpacked = Path.Combine(physical, "extracted", "share");

        File.Exists(unpacked).ShouldBeTrue();
        (await File.ReadAllTextAsync(unpacked, TestContext.Current.CancellationToken))
            .ShouldBe("the 1.2.0 binary");
    }

    [Fact]
    public async Task ExtractAsync_Should_Fail_OnBytesThatAreNotAnArchive()
    {
        Directory.CreateDirectory(_root);

        string archive = Path.Combine(_root, "share-1.2.0-linux-x64.tar.gz");

        await File.WriteAllTextAsync(
            archive,
            "not an archive",
            TestContext.Current.CancellationToken);

        Result result = await ArchiveExtractor.ExtractAsync(
            archive,
            Path.Combine(_root, "extracted"),
            RuntimeIdentifier,
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.ArchiveUnreadable");
    }

    /// <summary>
    /// Writes the archive the release workflow packs: a gzipped tar carrying the <c>./</c>
    /// root entry, the executable and the settings file beside it.
    /// </summary>
    private async Task<string> GivenArchiveAsync(string content)
    {
        string staging = Path.Combine(_root, $"staging-{Guid.NewGuid():N}");

        Directory.CreateDirectory(staging);

        string executable = Path.Combine(staging, "share");
        string settings = Path.Combine(staging, "appsettings.json");

        await File.WriteAllTextAsync(executable, content, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(settings, "{}", TestContext.Current.CancellationToken);

        string archive = Path.Combine(_root, "share-1.2.0-linux-x64.tar.gz");

        await using (FileStream file = File.Create(archive))
        await using (var gzip = new GZipStream(file, CompressionMode.Compress))
        await using (var writer = new TarWriter(gzip, TarEntryFormat.Pax))
        {
            await writer.WriteEntryAsync(
                new PaxTarEntry(TarEntryType.Directory, "./"),
                TestContext.Current.CancellationToken);

            await writer.WriteEntryAsync(executable, "./share", TestContext.Current.CancellationToken);
            await writer.WriteEntryAsync(settings, "./appsettings.json", TestContext.Current.CancellationToken);
        }

        Directory.Delete(staging, recursive: true);

        return archive;
    }
}
