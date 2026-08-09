using FluentValidation;
using Share.Domain.Configuration;

namespace Share.Application.Configuration.Create;

internal sealed class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceCommandValidator() =>
        RuleFor(command => command.Name)
            .Must(ConfigurationWorkspaces.IsValidName)
            .WithMessage(
                "A workspace name must start with a letter and may contain only letters, " +
                $"digits, '-' and '_', up to {ConfigurationWorkspaces.MaximumNameLength} " +
                $"characters. '{ConfigurationWorkspaces.ActiveKey}' is reserved.");
}
