using FluentValidation;

namespace Application.Shares.Create;

internal sealed class CreateShareCommandValidator : AbstractValidator<CreateShareCommand>
{
    private static readonly char[] PathSeparators = ['/', '\\'];

    public CreateShareCommandValidator()
    {
        RuleFor(c => c.OwnerUserId).NotEmpty();

        RuleFor(c => c.ConfiguredTtlMinutes).GreaterThan(0);

        RuleFor(c => c.Files).NotEmpty();

        RuleForEach(c => c.Files).ChildRules(file =>
        {
            file.RuleFor(f => f.RelativePath)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(1024)
                .Must(path => !path.Split(PathSeparators).Contains(".."))
                .WithMessage("'{PropertyName}' must not contain path traversal segments.");

            file.RuleFor(f => f.Size).GreaterThanOrEqualTo(0);

            file.RuleFor(f => f.ContentType).MaximumLength(255).When(f => f.ContentType is not null);
        });
    }
}
