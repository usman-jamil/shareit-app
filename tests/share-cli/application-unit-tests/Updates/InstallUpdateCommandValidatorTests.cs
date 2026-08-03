using FluentValidation.Results;
using Share.Application.Updates.Install;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Updates;

public sealed class InstallUpdateCommandValidatorTests
{
    private readonly InstallUpdateCommandValidator _validator = new();

    [Fact]
    public void Validate_Should_Pass_ForACompleteCommand()
    {
        ValidationResult result = _validator.Validate(
            new InstallUpdateCommand(UpdateData.Version("1.2.0"), "/usr/local/bin/share", 1234));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_Pass_WhenThereIsNoCallerToWaitFor()
    {
        ValidationResult result = _validator.Validate(
            new InstallUpdateCommand(UpdateData.Version("1.2.0"), "/usr/local/bin/share", 0));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Should_Fail_WithoutATarget(string target)
    {
        ValidationResult result = _validator.Validate(
            new InstallUpdateCommand(UpdateData.Version("1.2.0"), target, 1234));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(
            failure => failure.PropertyName == nameof(InstallUpdateCommand.TargetExecutablePath));
    }

    [Fact]
    public void Validate_Should_Fail_ForANegativeProcessId()
    {
        ValidationResult result = _validator.Validate(
            new InstallUpdateCommand(UpdateData.Version("1.2.0"), "/usr/local/bin/share", -1));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(
            failure => failure.PropertyName == nameof(InstallUpdateCommand.CallerProcessId));
    }
}
