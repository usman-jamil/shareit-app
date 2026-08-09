using Share.Application.Abstractions.Configuration;
using Share.Infrastructure.Configuration;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Infrastructure.UnitTests.Configuration;

/// <summary>
/// Exercises the real file, redirected with <c>SHARE_CLI_CONFIG</c> so the developer's own
/// <c>~/.share/config.yaml</c> is never touched.
/// </summary>
[Collection(ConfigurationFileCollection.Name)]
public sealed class YamlConfigurationStoreTests : IDisposable
{
    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");

    private readonly string _directory;
    private readonly string _path;
    private readonly string? _originalOverride;

    public YamlConfigurationStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"share-cli-tests-{Guid.NewGuid():N}");
        _path = Path.Combine(_directory, "config.yaml");

        _originalOverride =
            Environment.GetEnvironmentVariable(CliConfigurationPath.OverrideEnvironmentVariable);
        Environment.SetEnvironmentVariable(CliConfigurationPath.OverrideEnvironmentVariable, _path);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            CliConfigurationPath.OverrideEnvironmentVariable,
            _originalOverride);

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_Should_RoundTripTheApiKey()
    {
        var store = new YamlConfigurationStore();

        Result saved = await store.SaveAsync(
            new ShareApiSettings(new Uri("https://api.example.com"), 45, "sk_live_secret", UserId),
            TestContext.Current.CancellationToken);

        saved.IsSuccess.ShouldBeTrue();

        Result<ActiveWorkspace> read = await store.ReadAsync(TestContext.Current.CancellationToken);

        read.IsSuccess.ShouldBeTrue();
        read.Value.Settings.ApiKey.ShouldBe("sk_live_secret");
        read.Value.Settings.BaseUrl.ShouldBe(new Uri("https://api.example.com"));
        read.Value.Settings.TimeoutSeconds.ShouldBe(45);
        read.Value.Settings.UserId.ShouldBe(UserId);
    }

    [Fact]
    public async Task ReadAsync_Should_FailWithTheKeyName_WhenTheUserIdIsNotAnId()
    {
        await WriteFileAsync(
            """
            shareApi:
              userId: not-a-guid
            """);

        var store = new YamlConfigurationStore();

        Result<ActiveWorkspace> read = await store.ReadAsync(TestContext.Current.CancellationToken);

        read.IsFailure.ShouldBeTrue();
        read.Error.Code.ShouldBe("Configuration.InvalidValue");
        read.Error.Description.ShouldContain("shareApi.userId");
    }

    [Fact]
    public async Task ReadAsync_Should_ReturnNoApiKey_WhenTheFileDoesNotSetOne()
    {
        await WriteFileAsync(
            """
            shareApi:
              baseUrl: https://api.example.com
            """);

        var store = new YamlConfigurationStore();

        Result<ActiveWorkspace> read = await store.ReadAsync(TestContext.Current.CancellationToken);

        read.IsSuccess.ShouldBeTrue();
        read.Value.Settings.ApiKey.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_Should_TreatABlankApiKeyAsUnset()
    {
        await WriteFileAsync(
            """
            shareApi:
              apiKey: '   '
            """);

        var store = new YamlConfigurationStore();

        Result<ActiveWorkspace> read = await store.ReadAsync(TestContext.Current.CancellationToken);

        read.IsSuccess.ShouldBeTrue();
        read.Value.Settings.ApiKey.ShouldBeNull();
    }

    [Fact]
    public async Task SaveAsync_Should_LeaveUnrelatedKeysAlone()
    {
        await WriteFileAsync(
            """
            shareApi:
              apiKey: sk_live_secret
            somethingElse:
              kept: yes
            """);

        var store = new YamlConfigurationStore();

        Result saved = await store.SaveAsync(
            new ShareApiSettings(null, 45, "sk_live_secret", null),
            TestContext.Current.CancellationToken);

        saved.IsSuccess.ShouldBeTrue();

        string content = await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken);

        content.ShouldContain("somethingElse");
        content.ShouldContain("sk_live_secret");
    }

    [Fact]
    public async Task SaveAsync_Should_KeepTheFileReadableByItsOwnerOnly()
    {
        // The file holds an API key, so the permissions are part of the contract.
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");

            return;
        }

        var store = new YamlConfigurationStore();

        await store.SaveAsync(
            new ShareApiSettings(null, null, "sk_live_secret", null),
            TestContext.Current.CancellationToken);

        File.GetUnixFileMode(_path)
            .ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.GetUnixFileMode(_directory)
            .ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private async Task WriteFileAsync(string content)
    {
        Directory.CreateDirectory(_directory);

        await File.WriteAllTextAsync(_path, content, TestContext.Current.CancellationToken);
    }
}
