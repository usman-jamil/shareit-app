using FluentValidation.Results;
using Share.Application.Configuration.Create;
using Share.Domain.Configuration;
using Shouldly;
using Xunit;

namespace Share.Application.UnitTests.Configuration;

public class CreateWorkspaceCommandValidatorTests
{
    private readonly CreateWorkspaceCommandValidator _validator = new();

    private ValidationResult Validate(string name) =>
        _validator.Validate(new CreateWorkspaceCommand(name));

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
}
