using System.Globalization;
using System.Net;
using Share.Application.Abstractions.Updates;
using Share.Domain.Updates;
using Share.Infrastructure.Options;
using Share.Infrastructure.Updates;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Infrastructure.UnitTests.Updates;

/// <summary>
/// Pins how GitHub's release listing becomes releases the CLI can act on: which entries are
/// kept, which release "latest" means, and how every failure shape is reported.
/// </summary>
public sealed class GitHubReleaseCatalogTests : IDisposable
{
    private const string ListUrl =
        "https://api.github.test/repos/acme/share/releases?per_page=100";

    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (IDisposable disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task GetLatestAsync_Should_ReturnTheHighestStableRelease()
    {
        GitHubReleaseCatalog catalog = CatalogWith(Listing(
            Entry("sharecli-1.2.0"),
            Entry("sharecli-1.10.0"),
            Entry("sharecli-1.9.0")));

        Result<ReleaseInfo> result =
            await catalog.GetLatestAsync(TestContext.Current.CancellationToken);

        // Ordered by version, not by the order GitHub happened to return them, and not
        // as text — 1.10.0 is above 1.9.0.
        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ToString().ShouldBe("1.10.0");
        result.Value.TagName.ShouldBe("sharecli-1.10.0");
    }

    [Fact]
    public async Task GetLatestAsync_Should_SkipPreReleasesDraftsAndForeignTags()
    {
        GitHubReleaseCatalog catalog = CatalogWith(Listing(
            Entry("sharecli-1.0.0"),
            Entry("sharecli-2.0.0-beta.1", isPreRelease: true),
            Entry("sharecli-3.0.0", isDraft: true),

            // A release of something else published from the same repository.
            Entry("api-9.0.0"),
            Entry("sharecli-not-a-version")));

        Result<ReleaseInfo> result =
            await catalog.GetLatestAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ToString().ShouldBe("1.0.0");
    }

    [Fact]
    public async Task GetLatestAsync_Should_Fail_WhenNothingHasBeenPublished()
    {
        GitHubReleaseCatalog catalog = CatalogWith(Listing(
            Entry("sharecli-1.0.0-beta.1", isPreRelease: true)));

        Result<ReleaseInfo> result =
            await catalog.GetLatestAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.NoReleasesPublished");
    }

    [Fact]
    public async Task GetAsync_Should_MatchOnTheVersion_NotTheTagText()
    {
        GitHubReleaseCatalog catalog = CatalogWith(Listing(Entry("sharecli-v1.3.2")));

        Result<ReleaseInfo> result = await catalog.GetAsync(
            Version("1.3.2"),
            TestContext.Current.CancellationToken);

        // The release workflow accepts both `sharecli-1.3.2` and `sharecli-v1.3.2`, so the
        // user must not have to know which form was used.
        result.IsSuccess.ShouldBeTrue();
        result.Value.TagName.ShouldBe("sharecli-v1.3.2");
    }

    [Fact]
    public async Task GetAsync_Should_ReturnAPreRelease_WhenItIsAskedForByName()
    {
        GitHubReleaseCatalog catalog = CatalogWith(Listing(
            Entry("sharecli-1.0.0"),
            Entry("sharecli-2.0.0-beta.1", isPreRelease: true)));

        Result<ReleaseInfo> result = await catalog.GetAsync(
            Version("2.0.0-beta.1"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsPreRelease.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_Should_Fail_WhenThereIsNoSuchRelease()
    {
        GitHubReleaseCatalog catalog = CatalogWith(Listing(Entry("sharecli-1.0.0")));

        Result<ReleaseInfo> result = await catalog.GetAsync(
            Version("9.9.9"),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.ReleaseNotFound");
        result.Error.Description.ShouldContain("9.9.9");
    }

    [Fact]
    public async Task GetLatestAsync_Should_CarryTheAssetsThroughUnchanged()
    {
        GitHubReleaseCatalog catalog = CatalogWith(Listing(Entry("sharecli-1.0.0")));

        Result<ReleaseInfo> result =
            await catalog.GetLatestAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Assets.Count.ShouldBe(2);
        result.Value.Assets[0].Name.ShouldBe("share-1.0.0-linux-x64.tar.gz");
        result.Value.Assets[0].DownloadUrl.ShouldBe(
            new Uri("https://downloads.test/share-1.0.0-linux-x64.tar.gz"));
        result.Value.Assets[0].Size.ShouldBe(32_000_000);
        result.Value.Assets[1].Name.ShouldBe(ReleasePackaging.ChecksumsAssetName);
        result.Value.PublishedAt.ShouldBe(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        result.Value.ReleaseUrl.ShouldBe(
            new Uri("https://github.test/acme/share/releases/tag/sharecli-1.0.0"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "Update.RepositoryNotFound")]
    [InlineData(HttpStatusCode.Forbidden, "Update.CatalogRateLimited")]
    [InlineData(HttpStatusCode.TooManyRequests, "Update.CatalogRateLimited")]
    [InlineData(HttpStatusCode.InternalServerError, "Update.CatalogUnexpected")]
    [InlineData(HttpStatusCode.BadGateway, "Update.CatalogUnexpected")]
    public async Task GetLatestAsync_Should_MapTheStatus(HttpStatusCode status, string code)
    {
        GitHubReleaseCatalog catalog = CatalogWith(
            new StubRoutedHandler().Status(ListUrl, status));

        Result<ReleaseInfo> result =
            await catalog.GetLatestAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(code);
    }

    [Fact]
    public async Task GetLatestAsync_Should_Fail_WhenGitHubCannotBeReached()
    {
        GitHubReleaseCatalog catalog = CatalogWith(
            new StubRoutedHandler().Throws(
                ListUrl,
                new HttpRequestException("connection refused")));

        Result<ReleaseInfo> result =
            await catalog.GetLatestAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.CatalogUnreachable");
        result.Error.Description.ShouldContain("connection refused");
    }

    [Fact]
    public async Task GetLatestAsync_Should_Fail_OnAResponseThatIsNotAReleaseListing()
    {
        GitHubReleaseCatalog catalog = CatalogWith(
            new StubRoutedHandler().Json(ListUrl, """{"message":"Not Found"}"""));

        Result<ReleaseInfo> result =
            await catalog.GetLatestAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Update.CatalogInvalidResponse");
    }

    private GitHubReleaseCatalog CatalogWith(StubRoutedHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.test/")
        };

        _disposables.Add(handler);
        _disposables.Add(client);

        return new GitHubReleaseCatalog(
            client,
            Microsoft.Extensions.Options.Options.Create(new UpdateOptions
            {
                RepositoryOwner = "acme",
                RepositoryName = "share",
                TagPrefix = "sharecli-"
            }));
    }

    private static StubRoutedHandler Listing(params string[] entries) =>
        new StubRoutedHandler().Json(ListUrl, $"[{string.Join(',', entries)}]");

    private static string Entry(string tag, bool isPreRelease = false, bool isDraft = false)
    {
        string version = tag.StartsWith("sharecli-v", StringComparison.Ordinal)
            ? tag["sharecli-v".Length..]
            : tag["sharecli-".Length..];

        return string.Create(
            CultureInfo.InvariantCulture,
            $$"""
              {
                "tag_name": "{{tag}}",
                "draft": {{(isDraft ? "true" : "false")}},
                "prerelease": {{(isPreRelease ? "true" : "false")}},
                "published_at": "2026-01-02T03:04:05Z",
                "html_url": "https://github.test/acme/share/releases/tag/{{tag}}",
                "body": "notes the CLI never reads",
                "assets": [
                  {
                    "name": "share-{{version}}-linux-x64.tar.gz",
                    "browser_download_url": "https://downloads.test/share-{{version}}-linux-x64.tar.gz",
                    "size": 32000000
                  },
                  {
                    "name": "SHA256SUMS.txt",
                    "browser_download_url": "https://downloads.test/SHA256SUMS.txt",
                    "size": 562
                  }
                ]
              }
              """);
    }

    private static SemanticVersion Version(string text)
    {
        SemanticVersion.TryParse(text, out SemanticVersion? version).ShouldBeTrue();

        return version!;
    }
}
