using Microsoft.Extensions.Configuration;
using Share.Application.Abstractions.Configuration;
using Share.Domain.Configuration;
using Share.Infrastructure.Configuration;
using Share.Infrastructure.Options;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Infrastructure.UnitTests.Configuration;

/// <summary>
/// Covers the workspace layer of the configuration file: which section reads and writes
/// land in, and that the configuration provider binds the same one the store does.
/// </summary>
[Collection(ConfigurationFileCollection.Name)]
public sealed class WorkspaceConfigurationTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;
    private readonly string? _originalOverride;

    public WorkspaceConfigurationTests()
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
    public async Task ReadAsync_Should_UseTheDefaultWorkspace_WhenTheFileNamesNone()
    {
        // A file written before workspaces existed is already a valid one-workspace file.
        await WriteFileAsync(
            """
            shareApi:
              baseUrl: https://legacy.example.com
            """);

        Result<ActiveWorkspace> read = await Store().ReadAsync(TestContext.Current.CancellationToken);

        read.IsSuccess.ShouldBeTrue();
        read.Value.Name.ShouldBe(ConfigurationWorkspaces.DefaultName);
        read.Value.Settings.BaseUrl.ShouldBe(new Uri("https://legacy.example.com"));
    }

    [Fact]
    public async Task ReadAsync_Should_ReturnTheActiveWorkspacesValues_NotTheDefaultOnes()
    {
        await WriteWorkspacesAsync();

        Result<ActiveWorkspace> read = await Store().ReadAsync(TestContext.Current.CancellationToken);

        read.IsSuccess.ShouldBeTrue();
        read.Value.Name.ShouldBe("development");
        read.Value.Settings.BaseUrl.ShouldBe(new Uri("https://dev.example.com"));
        read.Value.Settings.ApiKey.ShouldBe("dev-key");
    }

    [Fact]
    public async Task ReadAsync_Should_Fail_WhenTheActiveWorkspaceIsNotDefined()
    {
        // Defaulting here would quietly aim the next command at localhost with no key.
        await WriteFileAsync(
            """
            active_workspace: staging
            shareApi:
              baseUrl: https://api.example.com
            """);

        Result<ActiveWorkspace> read = await Store().ReadAsync(TestContext.Current.CancellationToken);

        read.IsFailure.ShouldBeTrue();
        read.Error.Code.ShouldBe("Configuration.WorkspaceNotFound");
        read.Error.Description.ShouldContain("staging");
    }

    [Fact]
    public async Task ReadAsync_Should_NameTheActiveWorkspace_InAnInvalidValueFailure()
    {
        await WriteFileAsync(
            """
            active_workspace: development
            development:
              timeoutSeconds: soon
            """);

        Result<ActiveWorkspace> read = await Store().ReadAsync(TestContext.Current.CancellationToken);

        read.IsFailure.ShouldBeTrue();
        read.Error.Code.ShouldBe("Configuration.InvalidValue");
        read.Error.Description.ShouldContain("development.timeoutSeconds");
    }

    [Fact]
    public async Task ListWorkspacesAsync_Should_ReturnTheDefaultAlone_WhenThereIsNoFile()
    {
        Result<WorkspaceList> workspaces =
            await Store().ListWorkspacesAsync(TestContext.Current.CancellationToken);

        workspaces.IsSuccess.ShouldBeTrue();
        workspaces.Value.Active.ShouldBe(ConfigurationWorkspaces.DefaultName);
        workspaces.Value.Names.ShouldBe([ConfigurationWorkspaces.DefaultName]);
    }

    [Fact]
    public async Task ListWorkspacesAsync_Should_ReturnEverySection_AndTheDefaultEvenWhenAbsent()
    {
        await WriteFileAsync(
            """
            active_workspace: development
            development:
              baseUrl: https://dev.example.com
            production:
              baseUrl: https://api.example.com
            """);

        Result<WorkspaceList> workspaces =
            await Store().ListWorkspacesAsync(TestContext.Current.CancellationToken);

        workspaces.IsSuccess.ShouldBeTrue();
        workspaces.Value.Active.ShouldBe("development");
        workspaces.Value.Names.ShouldBe(
            [ConfigurationWorkspaces.DefaultName, "development", "production"]);
    }

    [Fact]
    public async Task ListWorkspacesAsync_Should_Succeed_WhenTheActiveWorkspaceIsNotDefined()
    {
        // Listing is how the user diagnoses that file, so it must not fail the way a read does.
        await WriteFileAsync("active_workspace: staging");

        Result<WorkspaceList> workspaces =
            await Store().ListWorkspacesAsync(TestContext.Current.CancellationToken);

        workspaces.IsSuccess.ShouldBeTrue();
        workspaces.Value.Active.ShouldBe("staging");
        workspaces.Value.Names.ShouldNotContain("staging");
    }

    [Fact]
    public async Task CreateWorkspaceAsync_Should_AddTheWorkspace_AndMakeItActive()
    {
        YamlConfigurationStore store = Store();

        Result created =
            await store.CreateWorkspaceAsync("development", TestContext.Current.CancellationToken);

        created.IsSuccess.ShouldBeTrue();

        Result<ActiveWorkspace> read = await store.ReadAsync(TestContext.Current.CancellationToken);

        read.IsSuccess.ShouldBeTrue();
        read.Value.Name.ShouldBe("development");
        read.Value.Settings.ShouldBe(ShareApiSettings.Empty);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_Should_Fail_WhenTheWorkspaceAlreadyExists()
    {
        await WriteWorkspacesAsync();

        Result created =
            await Store().CreateWorkspaceAsync("production", TestContext.Current.CancellationToken);

        created.IsFailure.ShouldBeTrue();
        created.Error.Code.ShouldBe("Configuration.WorkspaceAlreadyExists");
    }

    [Fact]
    public async Task CreateWorkspaceAsync_Should_Fail_ForTheDefaultWorkspace_WhichAlwaysExists()
    {
        Result created = await Store().CreateWorkspaceAsync(
            ConfigurationWorkspaces.DefaultName,
            TestContext.Current.CancellationToken);

        created.IsFailure.ShouldBeTrue();
        created.Error.Code.ShouldBe("Configuration.WorkspaceAlreadyExists");
    }

    [Fact]
    public async Task ActivateWorkspaceAsync_Should_Fail_WhenThereIsNoSuchWorkspace()
    {
        await WriteWorkspacesAsync();

        Result activated =
            await Store().ActivateWorkspaceAsync("staging", TestContext.Current.CancellationToken);

        activated.IsFailure.ShouldBeTrue();
        activated.Error.Code.ShouldBe("Configuration.WorkspaceNotFound");
    }

    [Fact]
    public async Task ActivateWorkspaceAsync_Should_MatchTheWorkspaceIgnoringCase()
    {
        await WriteWorkspacesAsync();

        Result activated =
            await Store().ActivateWorkspaceAsync("Production", TestContext.Current.CancellationToken);

        activated.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_Should_WriteIntoTheActiveWorkspace_AndLeaveTheOthersAlone()
    {
        await WriteWorkspacesAsync();

        YamlConfigurationStore store = Store();

        Result saved = await store.SaveAsync(
            new ShareApiSettings(new Uri("https://dev2.example.com"), 45, "dev-key-2", null),
            TestContext.Current.CancellationToken);

        saved.IsSuccess.ShouldBeTrue();

        Result<ActiveWorkspace> read = await store.ReadAsync(TestContext.Current.CancellationToken);

        read.Value.Name.ShouldBe("development");
        read.Value.Settings.BaseUrl.ShouldBe(new Uri("https://dev2.example.com"));

        await store.ActivateWorkspaceAsync("production", TestContext.Current.CancellationToken);

        Result<ActiveWorkspace> other = await store.ReadAsync(TestContext.Current.CancellationToken);

        other.Value.Settings.BaseUrl.ShouldBe(new Uri("https://api.example.com"));
        other.Value.Settings.ApiKey.ShouldBe("prod-key");
    }

    [Fact]
    public async Task CreateWorkspaceAsync_Should_LeaveTheWorkspaceTheUserWasOnUntouched()
    {
        await WriteFileAsync(
            """
            shareApi:
              baseUrl: https://api.example.com
              apiKey: prod-key
            """);

        YamlConfigurationStore store = Store();

        await store.CreateWorkspaceAsync("development", TestContext.Current.CancellationToken);
        await store.SaveAsync(
            new ShareApiSettings(new Uri("https://dev.example.com"), null, "dev-key", null),
            TestContext.Current.CancellationToken);
        await store.ActivateWorkspaceAsync(
            ConfigurationWorkspaces.DefaultName,
            TestContext.Current.CancellationToken);

        Result<ActiveWorkspace> read = await store.ReadAsync(TestContext.Current.CancellationToken);

        read.Value.Settings.BaseUrl.ShouldBe(new Uri("https://api.example.com"));
        read.Value.Settings.ApiKey.ShouldBe("prod-key");
    }

    [Fact]
    public async Task TheConfigurationProvider_Should_BindTheActiveWorkspace()
    {
        await WriteWorkspacesAsync();

        ShareApiOptions options = BindOptions();

        options.BaseUrl.ShouldBe(new Uri("https://dev.example.com"));
        options.ApiKey.ShouldBe("dev-key");
        options.TimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public async Task TheConfigurationProvider_Should_NotExposeTheInactiveWorkspaces()
    {
        // Their API keys have no business being in IConfiguration: nothing binds them, and
        // anything that dumps configuration would print them.
        await WriteWorkspacesAsync();

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddShareCliConfigurationFile()
            .Build();

        configuration.AsEnumerable()
            .Select(entry => entry.Value)
            .ShouldNotContain("prod-key");
    }

    [Fact]
    public async Task TheConfigurationProvider_Should_FallBackToDefaults_WhenTheActiveWorkspaceIsNotDefined()
    {
        // Startup must survive it — otherwise no command, not even `config list`, could run
        // to fix the file.
        await WriteFileAsync(
            """
            active_workspace: staging
            shareApi:
              baseUrl: https://api.example.com
            """);

        ShareApiOptions options = BindOptions();

        options.BaseUrl.ShouldBe(ShareApiDefaults.BaseUrl);
    }

    [Fact]
    public async Task SaveAsync_Should_WriteACreatedWorkspaceAsBlockYaml()
    {
        // A workspace is created empty, an empty mapping can only be written as `{}`, and a
        // flow node stays flow once it has settings in it unless the write forces it back.
        YamlConfigurationStore store = Store();

        await store.CreateWorkspaceAsync("development", TestContext.Current.CancellationToken);
        await store.SaveAsync(
            new ShareApiSettings(new Uri("https://dev.example.com"), null, null, null),
            TestContext.Current.CancellationToken);

        string content = await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken);

        content.ShouldContain($"development:{Environment.NewLine}  baseUrl:");
    }

    private static YamlConfigurationStore Store() => new();

    private static ShareApiOptions BindOptions()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddShareCliConfigurationFile()
            .Build();

        var options = new ShareApiOptions();
        configuration.GetSection(ShareApiOptions.SectionName).Bind(options);

        return options;
    }

    private Task WriteWorkspacesAsync() =>
        WriteFileAsync(
            """
            active_workspace: development
            shareApi:
              baseUrl: https://legacy.example.com
            development:
              baseUrl: https://dev.example.com
              apiKey: dev-key
              timeoutSeconds: 30
            production:
              baseUrl: https://api.example.com
              apiKey: prod-key
            """);

    private async Task WriteFileAsync(string content)
    {
        Directory.CreateDirectory(_directory);

        await File.WriteAllTextAsync(_path, content, TestContext.Current.CancellationToken);
    }
}
