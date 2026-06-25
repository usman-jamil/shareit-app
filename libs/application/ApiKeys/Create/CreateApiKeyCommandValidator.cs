using FluentValidation;

namespace Application.ApiKeys.Create;

internal sealed class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Label).MaximumLength(100).When(c => c.Label is not null);
    }
}
