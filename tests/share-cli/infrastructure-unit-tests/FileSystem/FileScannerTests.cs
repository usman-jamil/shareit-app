using Share.Application.Abstractions.FileSystem;
using Share.Infrastructure.FileSystem;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Infrastructure.UnitTests.FileSystem;

/// <summary>
/// Walks real directories in a temporary folder — the point of this class is what the file
/// system actually does, so stubbing it away would test nothing.
/// </summary>
public sealed class FileScannerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"share-cli-scan-{Guid.NewGuid():N}");

    private readonly FileScanner _scanner = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void Given(string relativePath, string content = "x")
    {
        string path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void Scan_Should_ReturnEveryFileBeneathTheRoot_WithForwardSlashPaths()
    {
        Given("README.md");
        Given("docs/images/logo.png");
        Given("docs/report.pdf");

        Result<ScannedDirectory> result = _scanner.Scan(_root);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Files.Select(file => file.RelativePath)
            .ShouldBe(["README.md", "docs/images/logo.png", "docs/report.pdf"]);
    }

    [Fact]
    public void Scan_Should_ReportSizesAndInferredContentTypes()
    {
        Given("notes.txt", "hello");
        Given("archive.unknownext", "xyz");

        Result<ScannedDirectory> result = _scanner.Scan(_root);

        result.IsSuccess.ShouldBeTrue();

        LocalFile notes = result.Value.Files.Single(file => file.RelativePath == "notes.txt");
        notes.Size.ShouldBe(5);
        notes.ContentType.ShouldBe("text/plain");

        LocalFile unknown = result.Value.Files.Single(file => file.RelativePath == "archive.unknownext");
        unknown.ContentType.ShouldBeNull();
    }

    [Fact]
    public void Scan_Should_IncludeDotFilesAndDottedDirectories()
    {
        Given(".env");
        Given(".config/settings.json");

        Result<ScannedDirectory> result = _scanner.Scan(_root);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Files.Select(file => file.RelativePath)
            .ShouldBe([".config/settings.json", ".env"]);
    }

    [Fact]
    public void Scan_Should_ResolveTheRootToAnAbsolutePath()
    {
        Given("a.txt");

        Result<ScannedDirectory> result = _scanner.Scan(Path.Combine(_root, "docs", ".."));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Root.ShouldBe(_root);
        result.Value.Files[0].FullPath.ShouldBe(Path.Combine(_root, "a.txt"));
    }

    [Fact]
    public void Scan_Should_Fail_WhenTheDirectoryDoesNotExist()
    {
        Result<ScannedDirectory> result = _scanner.Scan(Path.Combine(_root, "missing"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Share.DirectoryNotFound");
    }

    [Fact]
    public void Scan_Should_Fail_WhenTheDirectoryHoldsNoFiles()
    {
        Directory.CreateDirectory(Path.Combine(_root, "empty"));

        Result<ScannedDirectory> result = _scanner.Scan(_root);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Share.DirectoryEmpty");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Scan_Should_Fail_WhenNoPathIsGiven(string directoryPath)
    {
        Result<ScannedDirectory> result = _scanner.Scan(directoryPath);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Share.DirectoryNotFound");
    }
}
