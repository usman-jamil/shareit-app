using System.Globalization;
using Share.Application.Configuration;
using Share.Application.Configuration.List;
using Spectre.Console;

namespace Share.Cli.Rendering;

/// <summary>
/// How the configuration commands put themselves on screen: a field list for one workspace,
/// a table for all of them.
/// </summary>
internal static class ConfigurationView
{
    /// <summary>
    /// The effective settings of the active workspace, with the values that are only there
    /// because nothing was set marked as defaults.
    /// </summary>
    public static void Write(ConfigurationResponse configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Grid fields = ConsoleOutput.Fields();

        fields.AddRow(ConsoleOutput.Label("File"), ConsoleOutput.Value(configuration.Location));
        fields.AddRow(
            string.Empty,
            ConsoleOutput.Muted(configuration.Exists
                ? "present"
                : "not created yet — all values are defaults"));
        fields.AddRow(
            ConsoleOutput.Label("Workspace"),
            $"[bold]{ConsoleOutput.Value(configuration.Workspace)}[/]");
        fields.AddEmptyRow();

        fields.AddRow(
            ConsoleOutput.Label("baseUrl"),
            Defaulted(configuration.BaseUrl.ToString(), configuration.BaseUrlIsDefault));
        fields.AddRow(
            ConsoleOutput.Label("timeoutSeconds"),
            Defaulted(
                configuration.TimeoutSeconds.ToString(CultureInfo.InvariantCulture),
                configuration.TimeoutSecondsIsDefault));

        // Presence only — printing the key would put a secret on the terminal, into
        // scrollback and into any transcript the user pastes when asking for help.
        fields.AddRow(
            ConsoleOutput.Label("apiKey"),
            configuration.ApiKeyIsSet ? "[green]set[/]" : ConsoleOutput.Muted("not set"));
        fields.AddRow(
            ConsoleOutput.Label("userId"),
            configuration.UserId is { } userId
                ? ConsoleOutput.Value(userId.ToString())
                : ConsoleOutput.Muted("not set"));

        ConsoleOutput.Write(fields);
    }

    /// <summary>
    /// Every workspace as a two-column table — the name, and the server it points at. The
    /// active one is marked in the name column rather than given a column of its own, so the
    /// table stays as wide as its content and no wider.
    /// </summary>
    public static void Write(WorkspacesResponse workspaces)
    {
        ArgumentNullException.ThrowIfNull(workspaces);

        Grid header = ConsoleOutput.Fields();

        header.AddRow(ConsoleOutput.Label("File"), ConsoleOutput.Value(workspaces.Location));
        header.AddRow(
            string.Empty,
            ConsoleOutput.Muted(workspaces.Exists
                ? "present"
                : "not created yet — all values are defaults"));

        ConsoleOutput.Write(header);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(Table(workspaces));

        // A hand-edited file can point at a workspace that is not there. Say so rather than
        // leaving the user to wonder why nothing is marked active.
        if (workspaces.ActiveIsMissing)
        {
            AnsiConsole.WriteLine();
            ConsoleOutput.Warn(
                $"The active workspace '{workspaces.Active}' is not defined in this file. " +
                $"Run `share config create {workspaces.Active}` to add it, or " +
                "`share config activate` to pick one of the above.");
        }
    }

    private static Table Table(WorkspacesResponse workspaces)
    {
        Table table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn("[bold]Name[/]")
            .AddColumn("[bold]Base URL[/]");

        foreach (WorkspaceView workspace in workspaces.Workspaces)
        {
            string name = ConsoleOutput.Value(workspace.Name);

            table.AddRow(
                workspace.IsActive ? $"[green]* {name}[/]" : $"  {name}",
                Defaulted(workspace.BaseUrl, workspace.BaseUrlIsDefault));
        }

        if (workspaces.Workspaces.Any(workspace => workspace.IsActive))
        {
            table.Caption("[dim]* active — `share config activate` to switch[/]");
        }

        return table;
    }

    private static string Defaulted(string value, bool isDefault) =>
        isDefault
            ? ConsoleOutput.Muted($"{value}  (default)")
            : ConsoleOutput.Value(value);
}
