using FluentValidation.Results;
using Share.Application.Abstractions.Configuration;
using Share.Application.Configuration.Create;
using Share.Domain.Configuration;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Configuration;

public class CreateWorkspaceCommandValidatorTests
{
    private readonly CreateWorkspaceCommandValidator _validator = new();

    private ValidationResult Validate(string name, ShareApiSettings? settings = null) =>
        _validator.Validate(new CreateWorkspaceCommand(name, settings));

    [Theory]
    [InlineData("development")]
    [InlineData("Production")]
    [InlineData("staging-eu")]
    [InlineData("test_2")]
    [InlineData(ConfigurationWorkspaces.DefaultName)]
    public void Validate_Should_Pass_ForANameThatIsAlsoAValidYamlKeyAndSectionName(string name) =>
        Validate(name).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2nd")]
    [InlineData("has space")]
    [InlineData("has:colon")]
    [InlineData("has.dot")]
    // Reserved: it is the root key naming the active workspace, not a workspace itself.
    [InlineData(ConfigurationWorkspaces.ActiveKey)]
    public void Validate_Should_Fail_ForANameThatCannotBeASection(string name) =>
        Validate(name).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_Should_Fail_WhenTheNameIsTooLong() =>
        Validate(new string('a', ConfigurationWorkspaces.MaximumNameLength + 1))
            .IsValid
            .ShouldBeFalse();

    [Fact]
    public void Validate_Should_Pass_WhenNoSettingsAreGiven() =>
        // `share config create <name>` creates a bare workspace; there is nothing to check.
        Validate("development").IsValid.ShouldBeTrue();

    [Fact]
    public void Validate_Should_Pass_ForSettingsThatAreAllUnset() =>
        Validate("development", ShareApiSettings.Empty).IsValid.ShouldBeTrue();

    [Fact]
    public void Validate_Should_Pass_ForCompleteSettings() =>
        Validate(
                "development",
                new ShareApiSettings(
                    new Uri("https://api.example.com"),
                    45,
                    "sk_live_key",
                    Guid.NewGuid()))
            .IsValid
            .ShouldBeTrue();

    [Fact]
    public void Validate_Should_Fail_ForABaseUrlThatIsNotHttp() =>
        Validate("development", Settings(baseUrl: new Uri("ftp://api.example.com")))
            .IsValid
            .ShouldBeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(3601)]
    public void Validate_Should_Fail_ForATimeoutOutsideTheAllowedRange(int timeoutSeconds) =>
        Validate("development", Settings(timeoutSeconds: timeoutSeconds)).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_Should_Fail_ForABlankApiKey()
    {
        ValidationResult result = Validate("development", Settings(apiKey: "   "));

        result.IsValid.ShouldBeFalse();

        // The message must not carry the value: it is a secret, and this one ends up on a
        // terminal.
        result.Errors.ShouldAllBe(error => !error.ErrorMessage.Contains("   ", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Should_Fail_ForAnEmptyUserId() =>
        Validate("development", Settings(userId: Guid.Empty)).IsValid.ShouldBeFalse();

    private static ShareApiSettings Settings(
        Uri? baseUrl = null,
        int? timeoutSeconds = null,
        string? apiKey = null,
        Guid? userId = null) =>
        new(baseUrl, timeoutSeconds, apiKey, userId);
}
