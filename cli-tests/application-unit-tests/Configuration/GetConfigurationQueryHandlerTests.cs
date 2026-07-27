using NSubstitute;
using Share.Application.Abstractions.Configuration;
using Share.Application.Configuration;
using Share.Application.Configuration.Get;
using Share.Domain.Configuration;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Configuration;

public class GetConfigurationQueryHandlerTests
{
    private const string Location = "/home/test/.share/config.yaml";

    private readonly IConfigurationStore _store = Substitute.For<IConfigurationStore>();
    private readonly GetConfigurationQueryHandler _handler;

    public GetConfigurationQueryHandlerTests()
    {
        _store.Location.Returns(Location);

        _handler = new GetConfigurationQueryHandler(_store);
    }

    private Task<Result<ConfigurationResponse>> Handle() =>
        _handler.Handle(new GetConfigurationQuery(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_Should_ReportDefaults_WhenTheFileSetsNothing()
    {
        _store.Exists.Returns(false);
        _store.ReadAsync(Arg.Any<CancellationToken>()).Returns(Result.Success(ShareApiSettings.Empty));

        Result<ConfigurationResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Location.ShouldBe(Location);
        result.Value.Exists.ShouldBeFalse();
        result.Value.BaseUrl.ShouldBe(ShareApiDefaults.BaseUrl);
        result.Value.BaseUrlIsDefault.ShouldBeTrue();
        result.Value.TimeoutSeconds.ShouldBe(ShareApiDefaults.TimeoutSeconds);
        result.Value.TimeoutSecondsIsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReportTheFileValues_AndMarkOnlyTheUnsetOnesDefaulted()
    {
        var baseUrl = new Uri("https://api.example.com");
        _store.Exists.Returns(true);
        _store
            .ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ShareApiSettings(baseUrl, null)));

        Result<ConfigurationResponse> result = await Handle();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Exists.ShouldBeTrue();
        result.Value.BaseUrl.ShouldBe(baseUrl);
        result.Value.BaseUrlIsDefault.ShouldBeFalse();
        result.Value.TimeoutSeconds.ShouldBe(ShareApiDefaults.TimeoutSeconds);
        result.Value.TimeoutSecondsIsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnTheStoreFailure_WhenTheFileCannotBeParsed()
    {
        Error error = ConfigurationErrors.Unparseable(Location, "mapping values are not allowed");
        _store
            .ReadAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ShareApiSettings>(error));

        Result<ConfigurationResponse> result = await Handle();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }
}
