using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Share.Application.Abstractions.Updates;
using Share.Domain.Updates;
using Share.Infrastructure.Updates;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Infrastructure.UnitTests.Updates;

/// <summary>
/// Exercises the whole staging step against a real archive: a genuine gzipped tar is built
/// in the test, served over a stubbed socket, and unpacked by the same code a release goes
/// through. What is being pinned is that a download only becomes an installable binary when
/// its SHA-256 matches what the release published.
/// </summary>
public sealed class UpdatePackageInstallerTests : IDisposable
{
    private const string RuntimeIdentifier = "linux-x64";
    private const string ArchiveUrl = "https://downloads.test/share-1.2.0-linux-x64.tar.gz";
    private const string ChecksumsUrl = "https://downloads.test/SHA256SUMS.txt";
    private const string ArchiveName = "share-1.2.0-linux-x64.tar.gz";
    private const string NewBinary = "the 1.2.0 binary";

    private readonly List<IDisposable> _disposables = [];

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"share-cli-installer-{Guid.NewGuid():N}");

    private readonly List<StagedUpdate> _staged = [];

    public void Dispose()
    {
        foreach (StagedUpdate staged in _staged)
        {
            if (Directory.Exists(staged.Directory))
            {
                Directory.Delete(staged.Directory, recursive: true);
            }
        }

        foreach (IDisposable disposable in _disposables)
        {
            disposable.Dispose();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task StageAsync_Should_UnpackTheBinary_WhenTheChecksumMatches()
    {
        byte[] archive = TarGz(NewBinary);

        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler()
            .Text(ChecksumsUrl, Sums(archive, ArchiveName))
            .Bytes(ArchiveUrl, archive));

        Result<StagedUpdate> result = await Stage(installer, Release());

        result.IsSuccess.ShouldBeTrue();
        Track(result.Value);

        File.Exists(result.Value.ExecutablePath).ShouldBeTrue();
        (await File.ReadAllTextAsync(
            result.Value.ExecutablePath,
            TestContext.Current.CancellationToken)).ShouldBe(NewBinary);
        Path.GetFileName(result.Value.ExecutablePath).ShouldBe("share");
    }

    [Fact]
    public async Task StageAsync_Should_FetchTheChecksums_BeforeTheArchive()
    {
        byte[] archive = TarGz(NewBinary);

        StubRoutedHandler handler = new StubRoutedHandler()
            .Text(ChecksumsUrl, Sums(archive, ArchiveName))
            .Bytes(ArchiveUrl, archive);

        Result<StagedUpdate> result = await Stage(InstallerWith(handler), Release());

        Track(result.Value);

        // Fetching them afterwards would mean a 35 MB download could still turn out to be
        // unverifiable.
        handler.Requests[0].ToString().ShouldBe(ChecksumsUrl);
        handler.Requests[1].ToString().ShouldBe(ArchiveUrl);
    }

    [Fact]
    public async Task StageAsync_Should_Refuse_WhenTheBytesDoNotMatchTheChecksum()
    {
        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler()
            .Text(ChecksumsUrl, Sums(TarGz("the release the project published"), ArchiveName))
            .Bytes(ArchiveUrl, TarGz("something else entirely")));

        Result<StagedUpdate> result = await Stage(installer, Release());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.ChecksumMismatch");
        result.Error.Description.ShouldContain(ArchiveName);
    }

    [Fact]
    public async Task StageAsync_Should_LeaveNothingBehind_WhenItFails()
    {
        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler()
            .Text(ChecksumsUrl, Sums(TarGz("expected"), ArchiveName))
            .Bytes(ArchiveUrl, TarGz("actual")));

        string[] before = Workspace();

        Result<StagedUpdate> result = await Stage(installer, Release());

        result.IsFailure.ShouldBeTrue();
        Workspace().ShouldBe(before);
    }

    [Fact]
    public async Task StageAsync_Should_Refuse_WhenTheChecksumsDoNotListTheArchive()
    {
        byte[] archive = TarGz(NewBinary);

        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler()
            .Text(ChecksumsUrl, Sums(archive, "share-1.2.0-win-x64.zip"))
            .Bytes(ArchiveUrl, archive));

        Result<StagedUpdate> result = await Stage(installer, Release());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.ChecksumMissing");
    }

    [Fact]
    public async Task StageAsync_Should_Refuse_WhenTheReleasePublishesNoChecksums()
    {
        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler());

        var release = new ReleaseInfo(
            Version("1.2.0"),
            "sharecli-1.2.0",
            IsPreRelease: false,
            PublishedAt: null,
            ReleaseUrl: null,
            [new ReleaseAsset(ArchiveName, new Uri(ArchiveUrl), 10)]);

        Result<StagedUpdate> result = await Stage(installer, release);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.ChecksumsUnavailable");
    }

    [Fact]
    public async Task StageAsync_Should_Refuse_WhenNoArchiveIsPublishedForThisMachine()
    {
        UpdatePackageInstaller installer = InstallerWith(
            new StubRoutedHandler(),
            runtimeIdentifier: "osx-arm64");

        Result<StagedUpdate> result = await Stage(installer, Release());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.AssetNotFound");
        result.Error.Description.ShouldContain("share-1.2.0-osx-arm64.tar.gz");
    }

    [Fact]
    public async Task StageAsync_Should_Refuse_WhenThePlatformHasNoRuntimeIdentifier()
    {
        UpdatePackageInstaller installer = InstallerWith(
            new StubRoutedHandler(),
            runtimeIdentifier: null);

        Result<StagedUpdate> result = await Stage(installer, Release());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.UnsupportedPlatform");
    }

    [Fact]
    public async Task StageAsync_Should_ReportTheStatus_WhenTheDownloadIsRejected()
    {
        byte[] archive = TarGz(NewBinary);

        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler()
            .Text(ChecksumsUrl, Sums(archive, ArchiveName))
            .Status(ArchiveUrl, HttpStatusCode.Forbidden));

        Result<StagedUpdate> result = await Stage(installer, Release());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.DownloadRejected");
        result.Error.Description.ShouldContain("403");
    }

    [Fact]
    public async Task StageAsync_Should_Fail_WhenTheConnectionDies()
    {
        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler()
            .Throws(ChecksumsUrl, new HttpRequestException("connection refused")));

        Result<StagedUpdate> result = await Stage(installer, Release());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.DownloadFailed");
        result.Error.Description.ShouldContain("connection refused");
    }

    [Fact]
    public async Task StageAsync_Should_Fail_WhenTheArchiveDoesNotHoldTheBinary()
    {
        byte[] archive = TarGz(NewBinary, entryName: "readme.txt");

        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler()
            .Text(ChecksumsUrl, Sums(archive, ArchiveName))
            .Bytes(ArchiveUrl, archive));

        Result<StagedUpdate> result = await Stage(installer, Release());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.ExecutableMissing");
    }

    [Fact]
    public async Task StageAsync_Should_Fail_OnBytesThatAreNotAnArchive()
    {
        byte[] rubbish = [1, 2, 3, 4, 5];

        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler()
            .Text(ChecksumsUrl, Sums(rubbish, ArchiveName))
            .Bytes(ArchiveUrl, rubbish));

        Result<StagedUpdate> result = await Stage(installer, Release());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.ArchiveUnreadable");
    }

    [Fact]
    public async Task ReplaceAsync_Should_PutTheStagedBinaryInPlace()
    {
        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler());

        string target = Given("share", "the 1.0.0 binary");
        StagedUpdate staged = GivenStaged(NewBinary);

        Result result = await installer.ReplaceAsync(
            staged,
            target,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        (await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken))
            .ShouldBe(NewBinary);

        // The rename is within the target's own directory, so nothing is left beside it.
        Directory.GetFiles(Path.GetDirectoryName(target)!).Length.ShouldBe(1);
    }

    [Fact]
    public async Task ReplaceAsync_Should_KeepTheInstalledBinarysPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler());

        string target = Given("share", "the 1.0.0 binary");
        const UnixFileMode Installed =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute;

        File.SetUnixFileMode(target, Installed);

        Result result = await installer.ReplaceAsync(
            GivenStaged(NewBinary),
            target,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        File.GetUnixFileMode(target).ShouldBe(Installed);
    }

    [Fact]
    public async Task ReplaceAsync_Should_Fail_WhenTheTargetDirectoryDoesNotExist()
    {
        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler());

        Result result = await installer.ReplaceAsync(
            GivenStaged(NewBinary),
            Path.Combine(_root, "nowhere", "share"),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.TargetNotWritable");
    }

    [Fact]
    public async Task Discard_Should_RemoveEverythingStagingCreated()
    {
        byte[] archive = TarGz(NewBinary);

        UpdatePackageInstaller installer = InstallerWith(new StubRoutedHandler()
            .Text(ChecksumsUrl, Sums(archive, ArchiveName))
            .Bytes(ArchiveUrl, archive));

        Result<StagedUpdate> staged = await Stage(installer, Release());

        installer.Discard(staged.Value);

        Directory.Exists(staged.Value.Directory).ShouldBeFalse();
    }

    private static Task<Result<StagedUpdate>> Stage(
        UpdatePackageInstaller installer,
        ReleaseInfo release) =>
        installer.StageAsync(release, TestContext.Current.CancellationToken);

    private void Track(StagedUpdate staged) => _staged.Add(staged);

    private static string[] Workspace() =>
        Directory.Exists(UpdateWorkspaceRoot)
            ? [.. Directory.GetDirectories(UpdateWorkspaceRoot).Order()]
            : [];

    private static string UpdateWorkspaceRoot =>
        Path.Combine(Path.GetTempPath(), "share-cli-update");

    private UpdatePackageInstaller InstallerWith(
        StubRoutedHandler handler,
        string? runtimeIdentifier = RuntimeIdentifier)
    {
        var client = new HttpClient(handler);

        _disposables.Add(handler);
        _disposables.Add(client);

        return new UpdatePackageInstaller(
            client,
            new StubApplicationEnvironment(runtimeIdentifier));
    }

    private string Given(string name, string content)
    {
        string directory = Path.Combine(_root, $"install-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, name);

        File.WriteAllText(path, content);

        return path;
    }

    private StagedUpdate GivenStaged(string content)
    {
        string executable = Given("share", content);

        return new StagedUpdate(executable, Path.GetDirectoryName(executable)!);
    }

    private static ReleaseInfo Release() =>
        new(
            Version("1.2.0"),
            "sharecli-1.2.0",
            IsPreRelease: false,
            PublishedAt: null,
            ReleaseUrl: null,
            [
                new ReleaseAsset(ArchiveName, new Uri(ArchiveUrl), 10),
                new ReleaseAsset(
                    ReleasePackaging.ChecksumsAssetName,
                    new Uri(ChecksumsUrl),
                    562)
            ]);

    /// <summary>
    /// Builds the same shape the release workflow packs: a gzipped tar holding the
    /// executable at its root.
    /// </summary>
    private static byte[] TarGz(string content, string entryName = "share")
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"share-cli-archive-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, entryName), content);

            using var buffer = new MemoryStream();

            using (var gzip = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
            {
                TarFile.CreateFromDirectory(directory, gzip, includeBaseDirectory: false);
            }

            return buffer.ToArray();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// A <c>SHA256SUMS.txt</c> body, in the format <c>sha256sum</c> writes — lower-case
    /// hex, which the installer has to match against its own upper-case digest.
    /// </summary>
    private static string Sums(byte[] archive, string fileName) =>
        $"{Convert.ToHexStringLower(SHA256.HashData(archive))}  {fileName}\n";

    private static SemanticVersion Version(string text)
    {
        SemanticVersion.TryParse(text, out SemanticVersion? version).ShouldBeTrue();

        return version!;
    }
}
