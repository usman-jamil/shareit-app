using FluentValidation;

namespace Application.ApiKeys.Revoke;

internal sealed class RevokeApiKeyCommandValidator : AbstractValidator<RevokeApiKeyCommand>
{
    public RevokeApiKeyCommandValidator()
    {
        RuleFor(c => c.ApiKey).NotEmpty();
    }
}
