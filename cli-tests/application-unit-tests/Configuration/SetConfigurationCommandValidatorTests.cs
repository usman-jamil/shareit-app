using FluentValidation.Results;
using Share.Application.Configuration.Set;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Configuration;

public class SetConfigurationCommandValidatorTests
{
    private readonly SetConfigurationCommandValidator _validator = new();

    private ValidationResult Validate(Uri? baseUrl, int? timeoutSeconds) =>
        _validator.Validate(new SetConfigurationCommand(baseUrl, timeoutSeconds));

    [Fact]
    public void Validate_Should_Fail_WhenNothingIsBeingSet()
    {
        ValidationResult result = Validate(baseUrl: null, timeoutSeconds: null);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage == "Specify at least one setting to update.");
    }

    [Theory]
    [InlineData("api.example.com")]
    [InlineData("/shares")]
    public void Validate_Should_Fail_WhenBaseUrlIsRelative(string baseUrl)
    {
        ValidationResult result = Validate(new Uri(baseUrl, UriKind.Relative), timeoutSeconds: null);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_Should_Fail_WhenBaseUrlIsNotHttp()
    {
        ValidationResult result = Validate(new Uri("ftp://api.example.com"), timeoutSeconds: null);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public void Validate_Should_Fail_WhenTimeoutIsOutOfRange(int timeoutSeconds)
    {
        ValidationResult result = Validate(baseUrl: null, timeoutSeconds);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(45)]
    [InlineData(3600)]
    public void Validate_Should_Pass_WhenTimeoutIsInRange(int timeoutSeconds)
    {
        ValidationResult result = Validate(baseUrl: null, timeoutSeconds);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_Pass_WhenBothSettingsAreValid()
    {
        ValidationResult result = Validate(new Uri("https://api.example.com"), timeoutSeconds: 45);

        result.IsValid.ShouldBeTrue();
    }
}
