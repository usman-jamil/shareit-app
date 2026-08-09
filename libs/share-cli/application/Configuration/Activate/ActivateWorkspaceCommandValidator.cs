using FluentValidation;

namespace Share.Application.Configuration.Activate;

/// <summary>
/// Only checks that a name was given. Whether it names a real workspace is the store's
/// answer to give, so a typo comes back as <c>Configuration.WorkspaceNotFound</c> with the
/// list of workspaces that do exist.
/// </summary>
internal sealed class ActivateWorkspaceCommandValidator
    : AbstractValidator<ActivateWorkspaceCommand>
{
    public ActivateWorkspaceCommandValidator() =>
        RuleFor(command => command.Name)
            .NotEmpty()
            .WithMessage("Name the workspace to activate.");
}
