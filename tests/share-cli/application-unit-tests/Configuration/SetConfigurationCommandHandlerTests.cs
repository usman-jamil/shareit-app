using NSubstitute;
using Share.Application.Abstractions.Configuration;
using Share.Application.Configuration;
using Share.Application.Configuration.Set;
using Share.Domain.Configuration;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Configuration;

public class SetConfigurationCommandHandlerTests
{
    private const string Location = "/home/test/.share/config.yaml";
    private const string ExistingApiKey = "existing-key";

    private static readonly Uri ExistingBaseUrl = new("https://existing.example.com");
    private static readonly Uri NewBaseUrl = new("https://api.example.com");
    private static readonly Guid ExistingUserId = new("11111111-1111-1111-1111-111111111111");

    private readonly IConfigurationStore _store = Substitute.For<IConfigurationStore>();
    private readonly SetConfigurationCommandHandler _handler;

    public SetConfigurationCommandHandlerTests()
    {
        _store.Location.Returns(Location);
        _store
            .ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Active(new ShareApiSettings(ExistingBaseUrl, 30, ExistingApiKey, ExistingUserId)));
        _store.SaveAsync(Arg.Any<ShareApiSettings>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _handler = new SetConfigurationCommandHandler(_store);
    }

    private Task<Result<ConfigurationResponse>> Handle(
        Uri? baseUrl,
        int? timeoutSeconds,
        string? apiKey = null,
        Guid? userId = null) =>
        _handler.Handle(
            new SetConfigurationCommand(baseUrl, timeoutSeconds, apiKey, userId),
            TestContext.Current.CancellationToken);

    private static Result<ActiveWorkspace> Active(
        ShareApiSettings settings,
        string name = ConfigurationWorkspaces.DefaultName) =>
        Result.Success(new ActiveWorkspace(name, settings));

    [Fact]
    public async Task Handle_Should_KeepTheSettingsItWasNotAskedToChange()
    {
        ShareApiSettings? saved = null;
        _store
            .SaveAsync(Arg.Do<ShareApiSettings>(settings => saved = settings), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        Result<ConfigurationResponse> result = await Handle(baseUrl: null, timeoutSeconds: 45);

        result.IsSuccess.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved!.BaseUrl.ShouldBe(ExistingBaseUrl);
        saved.TimeoutSeconds.ShouldBe(45);
        saved.ApiKey.ShouldBe(ExistingApiKey);
    }

    [Fact]
    public async Task Handle_Should_ReportTheUpdatedConfigurationAsExisting()
    {
        Result<ConfigurationResponse> result = await Handle(NewBaseUrl, timeoutSeconds: null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Location.ShouldBe(Location);
        result.Value.Exists.ShouldBeTrue();
        result.Value.BaseUrl.ShouldBe(NewBaseUrl);
        result.Value.BaseUrlIsDefault.ShouldBeFalse();
        result.Value.TimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public async Task Handle_Should_WriteTheNewApiKey_WithoutDisturbingTheOtherSettings()
    {
        ShareApiSettings? saved = null;
        _store
            .SaveAsync(Arg.Do<ShareApiSettings>(settings => saved = settings), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        Result<ConfigurationResponse> result =
            await Handle(baseUrl: null, timeoutSeconds: null, apiKey: "new-key");

        result.IsSuccess.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved!.ApiKey.ShouldBe("new-key");
        saved.BaseUrl.ShouldBe(ExistingBaseUrl);
        saved.TimeoutSeconds.ShouldBe(30);
        saved.UserId.ShouldBe(ExistingUserId);
    }

    [Fact]
    public async Task Handle_Should_WriteTheNewUserId_AndReportItBack()
    {
        var newUserId = Guid.NewGuid();
        ShareApiSettings? saved = null;
        _store
            .SaveAsync(Arg.Do<ShareApiSettings>(settings => saved = settings), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        Result<ConfigurationResponse> result =
            await Handle(baseUrl: null, timeoutSeconds: null, userId: newUserId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(newUserId);
        saved.ShouldNotBeNull();
        saved!.UserId.ShouldBe(newUserId);
        saved.ApiKey.ShouldBe(ExistingApiKey);
    }

    [Fact]
    public async Task Handle_Should_ReportTheApiKeyAsSet_WithoutRevealingIt()
    {
        Result<ConfigurationResponse> result =
            await Handle(baseUrl: null, timeoutSeconds: null, apiKey: "super-secret");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApiKeyIsSet.ShouldBeTrue();
        result.Value.ToString().ShouldNotContain("super-secret");
    }

    [Fact]
    public async Task Handle_Should_ReportTheApiKeyAsNotSet_WhenTheFileHasNone()
    {
        _store
            .ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Active(new ShareApiSettings(ExistingBaseUrl, 30, null, ExistingUserId)));

        Result<ConfigurationResponse> result = await Handle(NewBaseUrl, timeoutSeconds: null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApiKeyIsSet.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_NotWrite_WhenTheExistingFileCannotBeRead()
    {
        Error error = ConfigurationErrors.Unparseable(Location, "mapping values are not allowed");
        _store
            .ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ActiveWorkspace>(error));

        Result<ConfigurationResponse> result = await Handle(NewBaseUrl, timeoutSeconds: null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        await _store
            .DidNotReceive()
            .SaveAsync(Arg.Any<ShareApiSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnTheStoreFailure_WhenTheWriteFails()
    {
        Error error = ConfigurationErrors.Unwritable(Location, "permission denied");
        _store
            .SaveAsync(Arg.Any<ShareApiSettings>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        Result<ConfigurationResponse> result = await Handle(NewBaseUrl, timeoutSeconds: null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }
}
