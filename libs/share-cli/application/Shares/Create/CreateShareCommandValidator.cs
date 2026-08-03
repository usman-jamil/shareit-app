using FluentValidation;

namespace Share.Application.Shares.Create;

internal sealed class CreateShareCommandValidator : AbstractValidator<CreateShareCommand>
{
    private const int MinimumTtlMinutes = 1;

    public CreateShareCommandValidator()
    {
        // Whether the directory exists is the scanner's business — this only rules out an
        // input that could never name one.
        RuleFor(command => command.DirectoryPath).NotEmpty();

        RuleFor(command => command.OwnerUserId)
            .NotEqual(Guid.Empty)
            .When(command => command.OwnerUserId is not null)
            .WithMessage("UserId must not be empty.");

        RuleFor(command => command.TtlMinutes)
            .GreaterThanOrEqualTo(MinimumTtlMinutes)
            .When(command => command.TtlMinutes is not null);
    }
}
