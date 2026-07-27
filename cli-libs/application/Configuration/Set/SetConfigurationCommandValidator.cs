using FluentValidation;

namespace Share.Application.Configuration.Set;

internal sealed class SetConfigurationCommandValidator : AbstractValidator<SetConfigurationCommand>
{
    private const int MinimumTimeoutSeconds = 1;
    private const int MaximumTimeoutSeconds = 3600;

    public SetConfigurationCommandValidator()
    {
        RuleFor(command => command)
            .Must(command => command.BaseUrl is not null || command.TimeoutSeconds is not null)
            .WithName(nameof(SetConfigurationCommand))
            .WithMessage("Specify at least one setting to update.");

        RuleFor(command => command.BaseUrl)
            .Must(BeAnAbsoluteHttpUrl)
            .When(command => command.BaseUrl is not null)
            .WithMessage("BaseUrl must be an absolute http or https URL.");

        RuleFor(command => command.TimeoutSeconds)
            .Must(seconds => seconds is null or >= MinimumTimeoutSeconds and <= MaximumTimeoutSeconds)
            .WithMessage(
                $"TimeoutSeconds must be between {MinimumTimeoutSeconds} and {MaximumTimeoutSeconds}.");
    }

    private static bool BeAnAbsoluteHttpUrl(Uri? baseUrl) =>
        baseUrl is { IsAbsoluteUri: true } &&
        (baseUrl.Scheme == Uri.UriSchemeHttp || baseUrl.Scheme == Uri.UriSchemeHttps);
}
