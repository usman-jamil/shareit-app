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

    private static readonly Uri ExistingBaseUrl = new("https://existing.example.com");
    private static readonly Uri NewBaseUrl = new("https://api.example.com");

    private readonly IConfigurationStore _store = Substitute.For<IConfigurationStore>();
    private readonly SetConfigurationCommandHandler _handler;

    public SetConfigurationCommandHandlerTests()
    {
        _store.Location.Returns(Location);
        _store
            .ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ShareApiSettings(ExistingBaseUrl, 30)));
        _store.SaveAsync(Arg.Any<ShareApiSettings>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _handler = new SetConfigurationCommandHandler(_store);
    }

    private Task<Result<ConfigurationResponse>> Handle(Uri? baseUrl, int? timeoutSeconds) =>
        _handler.Handle(
            new SetConfigurationCommand(baseUrl, timeoutSeconds),
            TestContext.Current.CancellationToken);

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
    public async Task Handle_Should_NotWrite_WhenTheExistingFileCannotBeRead()
    {
        Error error = ConfigurationErrors.Unparseable(Location, "mapping values are not allowed");
        _store
            .ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ShareApiSettings>(error));

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
