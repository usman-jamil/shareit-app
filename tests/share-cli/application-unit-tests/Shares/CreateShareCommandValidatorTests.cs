using FluentValidation.Results;
using Share.Application.Shares.Create;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Shares;

public class CreateShareCommandValidatorTests
{
    private readonly CreateShareCommandValidator _validator = new();

    private ValidationResult Validate(
        string directoryPath = "/work/report",
        Guid? ownerUserId = null,
        int? ttlMinutes = null) =>
        _validator.Validate(new CreateShareCommand(directoryPath, ownerUserId, ttlMinutes));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Should_Fail_WhenTheDirectoryIsBlank(string directoryPath)
    {
        ValidationResult result = Validate(directoryPath);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_Should_Fail_WhenTheOwnerIsEmpty()
    {
        ValidationResult result = Validate(ownerUserId: Guid.Empty);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage == "UserId must not be empty.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_Should_Fail_WhenTheTtlIsNotPositive(int ttlMinutes)
    {
        ValidationResult result = Validate(ttlMinutes: ttlMinutes);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_Should_Pass_WhenOnlyADirectoryIsGiven()
    {
        // Both the owner and the TTL fall back — to the configuration file and to the
        // API's own default — so a bare path is the normal case.
        ValidationResult result = Validate();

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_Pass_WhenEverythingIsGiven()
    {
        ValidationResult result = Validate(ownerUserId: Guid.NewGuid(), ttlMinutes: 60);

        result.IsValid.ShouldBeTrue();
    }
}
