using FluentValidation;

namespace Share.Application.Updates.Install;

internal sealed class InstallUpdateCommandValidator : AbstractValidator<InstallUpdateCommand>
{
    public InstallUpdateCommandValidator()
    {
        RuleFor(command => command.Version).NotNull();

        // Whether the path names something that exists is the installer's business — this
        // only rules out an input that could never name a binary.
        RuleFor(command => command.TargetExecutablePath).NotEmpty();

        // Zero is allowed and means "nothing to wait for"; a negative identifier is not a
        // process.
        RuleFor(command => command.CallerProcessId).GreaterThanOrEqualTo(0);
    }
}
