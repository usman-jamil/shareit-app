using Share.Application.Abstractions.Configuration;
using Share.Application.Configuration.List;
using Share.Domain.Configuration;
using Spectre.Console;

namespace Share.Cli.Rendering;

/// <summary>
/// The questions the configuration commands ask when they were not told everything on the
/// command line.
/// </summary>
/// <remarks>
/// Every method here reads the keyboard, so callers must check
/// <see cref="ConsoleOutput.IsInteractive"/> first: under a pipe or in CI there is nobody to
/// answer and a prompt would hang until the job timed out.
/// <see cref="SelectWorkspace"/> needs <see cref="ConsoleOutput.CanRedraw"/> on top of that —
/// it draws a moving cursor over a list, and Spectre throws where it cannot.
/// </remarks>
internal static class ConfigPrompts
{
    private const int MinimumPageSize = 3;
    private const int MaximumPageSize = 10;

    /// <summary>
    /// Asks which workspace to switch to. The list is what the file actually defines, so a
    /// name that cannot work is not offered in the first place.
    /// </summary>
    public static WorkspaceView SelectWorkspace(IReadOnlyList<WorkspaceView> workspaces)
    {
        ArgumentNullException.ThrowIfNull(workspaces);

        return AnsiConsole.Prompt(
            new SelectionPrompt<WorkspaceView>()
                .Title("Which workspace should the CLI use?")
                .HighlightStyle(new Style(Color.Green))
                .PageSize(Math.Clamp(workspaces.Count, MinimumPageSize, MaximumPageSize))
                .MoreChoicesText(ConsoleOutput.Muted("Move up and down to see more workspaces"))
                .UseConverter(Describe)
                .AddChoices(workspaces));
    }

    /// <summary>
    /// Asks for everything a workspace needs. Only the name is required — the rest may be
    /// left blank, which leaves the setting unset rather than writing a blank one.
    /// </summary>
    public static PromptedWorkspace NewWorkspace()
    {
        AnsiConsole.MarkupLine("[bold]New workspace[/]");
        AnsiConsole.MarkupLine(
            ConsoleOutput.Muted(
                "Press Enter to leave a setting unset — `share config set` can fill it in later."));
        AnsiConsole.WriteLine();

        string name = AnsiConsole.Prompt(
            new TextPrompt<string>("Name:")
                .Validate(value => ConfigurationWorkspaces.IsValidName(value)
                    ? ValidationResult.Success()
                    : ValidationResult.Error(
                        "[red]Start with a letter, then letters, digits, '-' or '_'.[/]")));

        string baseUrl = AnsiConsole.Prompt(
            new TextPrompt<string>(
                    $"Base URL {ConsoleOutput.Muted($"(blank for {ShareApiDefaults.BaseUrl})")}:")
                .AllowEmpty()
                .Validate(value => IsBlank(value) || IsAbsoluteHttpUrl(value)
                    ? ValidationResult.Success()
                    : ValidationResult.Error(
                        "[red]Enter an absolute http or https URL, e.g. https://api.example.com[/]")));

        // Secret: the key is a credential, so it is not echoed — and it is never read back out
        // of the file afterwards either. Unvalidated because any non-blank string is a
        // plausible key, and a wrong one is the API's business to reject.
        string apiKey = AnsiConsole.Prompt(
            new TextPrompt<string>($"API key {ConsoleOutput.Muted("(hidden, blank to skip)")}:")
                .AllowEmpty()
                .Secret());

        string userId = AnsiConsole.Prompt(
            new TextPrompt<string>($"User ID {ConsoleOutput.Muted("(blank to skip)")}:")
                .AllowEmpty()
                .Validate(value => IsBlank(value) || IsAUserId(value)
                    ? ValidationResult.Success()
                    : ValidationResult.Error(
                        "[red]Enter an id, e.g. 11111111-1111-1111-1111-111111111111[/]")));

        AnsiConsole.WriteLine();

        return new PromptedWorkspace(
            name.Trim(),
            new ShareApiSettings(
                IsBlank(baseUrl) ? null : new Uri(baseUrl.Trim(), UriKind.Absolute),
                // Not asked for: a timeout is a detail with a sensible default, and asking
                // about it would put a question the user has no opinion on in the way.
                TimeoutSeconds: null,
                IsBlank(apiKey) ? null : apiKey.Trim(),
                IsBlank(userId) ? null : Guid.Parse(userId.Trim())));
    }

    private static string Describe(WorkspaceView workspace)
    {
        string name = ConsoleOutput.Value(workspace.Name);
        string suffix = workspace.IsActive ? " [green](active)[/]" : string.Empty;

        return $"{name} {ConsoleOutput.Muted(workspace.BaseUrl)}{suffix}";
    }

    private static bool IsBlank(string value) => string.IsNullOrWhiteSpace(value);

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? parsed) &&
        (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Rejects the empty id as well as a malformed one: it parses, but it names nobody, and the
    /// command validator would refuse it a moment later anyway.
    /// </summary>
    private static bool IsAUserId(string value) =>
        Guid.TryParse(value.Trim(), out Guid parsed) && parsed != Guid.Empty;
}

/// <summary>
/// What <see cref="ConfigPrompts.NewWorkspace"/> collected: a name, and however much of the
/// rest the user chose to give.
/// </summary>
internal sealed record PromptedWorkspace(string Name, ShareApiSettings Settings);
