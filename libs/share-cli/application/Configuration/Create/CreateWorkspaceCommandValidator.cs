using FluentValidation;
using Share.Domain.Configuration;

namespace Share.Application.Configuration.Create;

/// <summary>
/// The settings rules mirror <c>SetConfigurationCommandValidator</c> on purpose: a value
/// that could not be written by <c>config set</c> must not get in through <c>config create</c>
/// either.
/// </summary>
internal sealed class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    private const int MinimumTimeoutSeconds = 1;
    private const int MaximumTimeoutSeconds = 3600;

    public CreateWorkspaceCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(ConfigurationWorkspaces.IsValidName)
            .WithMessage(
                "A workspace name must start with a letter and may contain only letters, " +
                $"digits, '-' and '_', up to {ConfigurationWorkspaces.MaximumNameLength} " +
                $"characters. '{ConfigurationWorkspaces.ActiveKey}' is reserved.");

        When(command => command.Settings is not null, () =>
        {
            RuleFor(command => command.Settings!.BaseUrl)
                .Must(BeAnAbsoluteHttpUrl)
                .When(command => command.Settings!.BaseUrl is not null)
                .WithMessage("BaseUrl must be an absolute http or https URL.");

            RuleFor(command => command.Settings!.TimeoutSeconds)
                .Must(seconds => seconds is null or >= MinimumTimeoutSeconds and <= MaximumTimeoutSeconds)
                .WithMessage(
                    $"TimeoutSeconds must be between {MinimumTimeoutSeconds} and {MaximumTimeoutSeconds}.");

            // Spelled out rather than left to FluentValidation's default, which would
            // interpolate the offending value — and that value is a secret.
            RuleFor(command => command.Settings!.ApiKey)
                .Must(apiKey => !string.IsNullOrWhiteSpace(apiKey))
                .When(command => command.Settings!.ApiKey is not null)
                .WithMessage("ApiKey must not be blank.");

            RuleFor(command => command.Settings!.UserId)
                .NotEqual(Guid.Empty)
                .When(command => command.Settings!.UserId is not null)
                .WithMessage("UserId must not be empty.");
        });
    }

    private static bool BeAnAbsoluteHttpUrl(Uri? baseUrl) =>
        baseUrl is { IsAbsoluteUri: true } &&
        (baseUrl.Scheme == Uri.UriSchemeHttp || baseUrl.Scheme == Uri.UriSchemeHttps);
}
