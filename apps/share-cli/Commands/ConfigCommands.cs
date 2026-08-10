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
using Share.Cli.Rendering;
using Share.Domain.Configuration;
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
/// <para>
/// <c>create</c> and <c>activate</c> ask for what they were not told, but only when there is
/// a terminal to ask: given every argument they need, both run without a word, so a script
/// or a Dockerfile behaves exactly as it did before.
/// </para>
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
            return ConsoleOutput.Fail(result.Error);
        }

        ConfigurationView.Write(result.Value);

        return 0;
    }

    /// <summary>
    /// List the workspaces the configuration file defines and show which one is active.
    /// </summary>
    [Command("list")]
    public async Task<int> List(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        Result<WorkspacesResponse> result = await ListWorkspacesAsync(scope, cancellationToken);

        if (result.IsFailure)
        {
            return ConsoleOutput.Fail(result.Error);
        }

        ConfigurationView.Write(result.Value);

        return 0;
    }

    /// <summary>
    /// Add a workspace and make it active. Given a name it is created empty; given none, it asks.
    /// </summary>
    /// <remarks>
    /// Everything <c>config set</c> writes from now on lands in the new workspace, leaving the
    /// one you were on untouched.
    /// </remarks>
    /// <param name="name">Name of the new workspace, e.g. development. Omit to be asked for it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Command("create")]
    public async Task<int> Create(
        [Argument] string? name = null,
        CancellationToken cancellationToken = default)
    {
        CreateWorkspaceCommand command;

        if (name is null)
        {
            if (!ConsoleOutput.IsInteractive)
            {
                return ConsoleOutput.Fail(ConfigurationErrors.WorkspaceNameRequired("create"));
            }

            PromptedWorkspace prompted = ConfigPrompts.NewWorkspace();

            command = new CreateWorkspaceCommand(prompted.Name, prompted.Settings);
        }
        else
        {
            // Named on the command line, it stays a bare workspace with everything defaulted:
            // that is the form scripts use, and it must not start asking questions.
            command = new CreateWorkspaceCommand(name);
        }

        using IServiceScope scope = serviceProvider.CreateScope();

        ICommandHandler<CreateWorkspaceCommand, ConfigurationResponse> handler =
            scope.ServiceProvider
                .GetRequiredService<ICommandHandler<CreateWorkspaceCommand, ConfigurationResponse>>();

        Result<ConfigurationResponse> result = await handler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return ConsoleOutput.Fail(result.Error);
        }

        ConsoleOutput.Success(
            $"Created workspace '{result.Value.Workspace}' in {result.Value.Location} and made it active.");
        ConfigurationView.Write(result.Value);

        return 0;
    }

    /// <summary>
    /// Point the CLI at an existing workspace. Given no name, it offers a list to pick from.
    /// </summary>
    /// <param name="name">Name of the workspace to switch to. Omit to pick from a list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Command("activate")]
    public async Task<int> Activate(
        [Argument] string? name = null,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        if (name is null)
        {
            Result<string> chosen = await ChooseWorkspaceAsync(scope, cancellationToken);

            if (chosen.IsFailure)
            {
                return ConsoleOutput.Fail(chosen.Error);
            }

            name = chosen.Value;
        }

        ICommandHandler<ActivateWorkspaceCommand, ConfigurationResponse> handler =
            scope.ServiceProvider
                .GetRequiredService<ICommandHandler<ActivateWorkspaceCommand, ConfigurationResponse>>();

        Result<ConfigurationResponse> result =
            await handler.Handle(new ActivateWorkspaceCommand(name), cancellationToken);

        if (result.IsFailure)
        {
            return ConsoleOutput.Fail(result.Error);
        }

        ConsoleOutput.Success($"Active workspace is now '{result.Value.Workspace}'.");
        ConfigurationView.Write(result.Value);

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
            return ConsoleOutput.Fail(ConfigurationErrors.InvalidBaseUrl(baseUrl));
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
            return ConsoleOutput.Fail(result.Error);
        }

        ConsoleOutput.Success($"Updated {result.Value.Location}");
        ConfigurationView.Write(result.Value);

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
        // the file in order to fix it. Written plainly, not through Spectre — it is the one
        // command whose output is meant to be pasted into another one.
        using IServiceScope scope = serviceProvider.CreateScope();

        IConfigurationStore store = scope.ServiceProvider.GetRequiredService<IConfigurationStore>();

        Console.WriteLine(store.Location);
    }

    /// <summary>
    /// Asks which workspace to switch to. Fails rather than guessing when there is nobody to
    /// ask, or when the file defines nothing to choose between.
    /// </summary>
    private static async Task<Result<string>> ChooseWorkspaceAsync(
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        // A list has to be drawn, not just typed into, so this asks for more than the other
        // prompts do — see ConsoleOutput.CanRedraw.
        if (!ConsoleOutput.CanRedraw)
        {
            return Result.Failure<string>(
                ConfigurationErrors.WorkspaceNameRequired("activate"));
        }

        Result<WorkspacesResponse> workspaces = await ListWorkspacesAsync(scope, cancellationToken);

        if (workspaces.IsFailure)
        {
            return Result.Failure<string>(workspaces.Error);
        }

        // The listing always includes the default workspace, so an empty one means a file
        // that has been edited into a state with nothing selectable in it at all.
        return workspaces.Value.Workspaces.Count == 0
            ? Result.Failure<string>(ConfigurationErrors.NoWorkspaces(workspaces.Value.Location))
            : Result.Success(ConfigPrompts.SelectWorkspace(workspaces.Value.Workspaces).Name);
    }

    private static async Task<Result<WorkspacesResponse>> ListWorkspacesAsync(
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        IQueryHandler<ListWorkspacesQuery, WorkspacesResponse> handler =
            scope.ServiceProvider
                .GetRequiredService<IQueryHandler<ListWorkspacesQuery, WorkspacesResponse>>();

        return await handler.Handle(new ListWorkspacesQuery(), cancellationToken);
    }
}
