using System.Globalization;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.Messaging;
using Share.Application.Configuration;
using Share.Application.Configuration.Activate;
using Share.Application.Configuration.Create;
using Share.Application.Configuration.Get;
using Share.Application.Configuration.List;
using Share.Application.Configuration.Set;
using SharedKernel;

namespace Share.Cli.Commands;

/// <summary>
/// Reads and updates <c>~/.shareit/config.yaml</c>, the CLI's source of truth for how it
/// reaches the API.
/// </summary>
/// <remarks>
/// The file holds one <b>workspace</b> per server the CLI can be pointed at, and
/// <c>show</c> and <c>set</c> always act on the active one. Switching servers is
/// <c>config activate</c>; nothing else takes a workspace name.
/// </remarks>
public class ConfigCommands(IServiceProvider serviceProvider)
{
    /// <summary>
    /// Show the effective configuration of the active workspace and which values are defaulted.
    /// </summary>
    [Command("show")]
    public async Task<int> Show(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        IQueryHandler<GetConfigurationQuery, ConfigurationResponse> handler =
            scope.ServiceProvider
                .GetRequiredService<IQueryHandler<GetConfigurationQuery, ConfigurationResponse>>();

        Result<ConfigurationResponse> result =
            await handler.Handle(new GetConfigurationQuery(), cancellationToken);

        if (result.IsFailure)
        {
            return Fail(result.Error);
        }

        Write(result.Value);

        return 0;
    }

    /// <summary>
    /// List the workspaces the configuration file defines and show which one is active.
    /// </summary>
    [Command("list")]
    public async Task<int> List(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        IQueryHandler<ListWorkspacesQuery, WorkspacesResponse> handler =
            scope.ServiceProvider
                .GetRequiredService<IQueryHandler<ListWorkspacesQuery, WorkspacesResponse>>();

        Result<WorkspacesResponse> result =
            await handler.Handle(new ListWorkspacesQuery(), cancellationToken);

        if (result.IsFailure)
        {
            return Fail(result.Error);
        }

        Write(result.Value);

        return 0;
    }

    /// <summary>
    /// Add a workspace and make it active. Everything `config set` writes from now on lands
    /// in it, leaving the workspace you were on untouched.
    /// </summary>
    /// <param name="name">Name of the new workspace, e.g. development.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Command("create")]
    public async Task<int> Create([Argument] string name, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        ICommandHandler<CreateWorkspaceCommand, ConfigurationResponse> handler =
            scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateWorkspaceCommand, ConfigurationResponse>>();

        Result<ConfigurationResponse> result =
            await handler.Handle(new CreateWorkspaceCommand(name), cancellationToken);

        if (result.IsFailure)
        {
            return Fail(result.Error);
        }

        Console.WriteLine(
            $"Created workspace '{result.Value.Workspace}' in {result.Value.Location} and made it active.");
        Console.WriteLine();
        Write(result.Value);

        return 0;
    }

    /// <summary>
    /// Point the CLI at an existing workspace.
    /// </summary>
    /// <param name="name">Name of the workspace to switch to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Command("activate")]
    public async Task<int> Activate([Argument] string name, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        ICommandHandler<ActivateWorkspaceCommand, ConfigurationResponse> handler =
            scope.ServiceProvider
                .GetRequiredService<ICommandHandler<ActivateWorkspaceCommand, ConfigurationResponse>>();

        Result<ConfigurationResponse> result =
            await handler.Handle(new ActivateWorkspaceCommand(name), cancellationToken);

        if (result.IsFailure)
        {
            return Fail(result.Error);
        }

        Console.WriteLine($"Active workspace is now '{result.Value.Workspace}'.");
        Console.WriteLine();
        Write(result.Value);

        return 0;
    }

    /// <summary>
    /// Update the active workspace. Only the settings you pass are changed.
    /// </summary>
    /// <param name="baseUrl">-u, Root address of the Share API, e.g. https://api.example.com.</param>
    /// <param name="timeoutSeconds">-t, Per-request timeout in seconds (1-3600).</param>
    /// <param name="apiKey">-k, API key sent as X-Api-Key. Stored in the configuration file, which is owner-readable only.</param>
    /// <param name="userId">-i, Owner new shares are created for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Command("set")]
    public async Task<int> Set(
        string? baseUrl = null,
        int? timeoutSeconds = null,
        string? apiKey = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        Uri? parsedBaseUrl = null;

        if (baseUrl is not null &&
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out parsedBaseUrl))
        {
            await Console.Error.WriteLineAsync(
                $"'{baseUrl}' is not an absolute URL, e.g. https://api.example.com");

            return 1;
        }

        using IServiceScope scope = serviceProvider.CreateScope();

        ICommandHandler<SetConfigurationCommand, ConfigurationResponse> handler =
            scope.ServiceProvider
                .GetRequiredService<ICommandHandler<SetConfigurationCommand, ConfigurationResponse>>();

        Result<ConfigurationResponse> result = await handler.Handle(
            new SetConfigurationCommand(parsedBaseUrl, timeoutSeconds, apiKey, userId),
            cancellationToken);

        if (result.IsFailure)
        {
            return Fail(result.Error);
        }

        Console.WriteLine($"Updated {result.Value.Location}");
        Write(result.Value);

        return 0;
    }

    /// <summary>
    /// Print the path of the configuration file, whether or not it exists yet.
    /// </summary>
    [Command("path")]
    public void Path()
    {
        // Reads the store's location rather than going through a handler: this must keep
        // working when the file is missing or unparseable, since it is how the user finds
        // the file in order to fix it.
        using IServiceScope scope = serviceProvider.CreateScope();

        IConfigurationStore store = scope.ServiceProvider.GetRequiredService<IConfigurationStore>();

        Console.WriteLine(store.Location);
    }

    private static void Write(ConfigurationResponse configuration)
    {
        Console.WriteLine($"File            {configuration.Location}");
        Console.WriteLine(
            $"                {(configuration.Exists ? "present" : "not created yet — all values are defaults")}");
        Console.WriteLine($"Workspace       {configuration.Workspace}");
        Console.WriteLine();
        Console.WriteLine(
            $"baseUrl         {configuration.BaseUrl}{Suffix(configuration.BaseUrlIsDefault)}");
        Console.WriteLine(
            $"timeoutSeconds  {configuration.TimeoutSeconds.ToString(CultureInfo.InvariantCulture)}{Suffix(configuration.TimeoutSecondsIsDefault)}");

        // Presence only — printing the key would put a secret on the terminal, into scrollback
        // and into any transcript the user pastes when asking for help.
        Console.WriteLine(
            $"apiKey          {(configuration.ApiKeyIsSet ? "set" : "not set")}");
        Console.WriteLine(
            $"userId          {configuration.UserId?.ToString() ?? "not set"}");
    }

    private static void Write(WorkspacesResponse workspaces)
    {
        Console.WriteLine($"File            {workspaces.Location}");
        Console.WriteLine(
            $"                {(workspaces.Exists ? "present" : "not created yet — all values are defaults")}");
        Console.WriteLine();

        foreach (string name in workspaces.Names)
        {
            bool isActive = string.Equals(name, workspaces.Active, StringComparison.OrdinalIgnoreCase);

            Console.WriteLine($"{(isActive ? "*" : " ")} {name}");
        }

        // A hand-edited file can point at a workspace that is not there. Say so here rather
        // than leaving the user to wonder why nothing is marked active.
        if (workspaces.ActiveIsMissing)
        {
            Console.WriteLine();
            Console.Error.WriteLine(
                $"The active workspace '{workspaces.Active}' is not defined in this file. " +
                $"Run `share config create {workspaces.Active}` to add it, or " +
                "`share config activate <name>` to pick one of the above.");
        }
    }

    private static string Suffix(bool isDefault) => isDefault ? "  (default)" : string.Empty;

    private static int Fail(Error error)
    {
        // A validation failure carries one message per broken rule; show them all.
        if (error is ValidationError validationError)
        {
            foreach (Error inner in validationError.Errors)
            {
                Console.Error.WriteLine(inner.Description);
            }
        }
        else
        {
            Console.Error.WriteLine(error.Description);
        }

        return 1;
    }
}
