using FluentValidation;

namespace Share.Application.Configuration.Set;

internal sealed class SetConfigurationCommandValidator : AbstractValidator<SetConfigurationCommand>
{
    private const int MinimumTimeoutSeconds = 1;
    private const int MaximumTimeoutSeconds = 3600;

    public SetConfigurationCommandValidator()
    {
        RuleFor(command => command)
            .Must(command =>
                command.BaseUrl is not null ||
                command.TimeoutSeconds is not null ||
                command.ApiKey is not null ||
                command.UserId is not null)
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

        // The message is spelled out rather than left to FluentValidation's default, which
        // would interpolate the offending value — and that value is a secret.
        RuleFor(command => command.ApiKey)
            .Must(apiKey => !string.IsNullOrWhiteSpace(apiKey))
            .When(command => command.ApiKey is not null)
            .WithMessage("ApiKey must not be blank.");

        RuleFor(command => command.UserId)
            .NotEqual(Guid.Empty)
            .When(command => command.UserId is not null)
            .WithMessage("UserId must not be empty.");
    }

    private static bool BeAnAbsoluteHttpUrl(Uri? baseUrl) =>
        baseUrl is { IsAbsoluteUri: true } &&
        (baseUrl.Scheme == Uri.UriSchemeHttp || baseUrl.Scheme == Uri.UriSchemeHttps);
}
