using System.Globalization;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.Messaging;
using Share.Application.Configuration;
using Share.Application.Configuration.Get;
using Share.Application.Configuration.Set;
using SharedKernel;

namespace Share.Cli.Commands;

/// <summary>
/// Reads and updates <c>~/.share/config.yaml</c>, the CLI's source of truth for how it
/// reaches the API.
/// </summary>
public class ConfigCommands(IServiceProvider serviceProvider)
{
    /// <summary>
    /// Show the effective configuration and which values are defaulted.
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
    /// Update the configuration file. Only the settings you pass are changed.
    /// </summary>
    /// <param name="baseUrl">-u, Root address of the Share API, e.g. https://api.example.com.</param>
    /// <param name="timeoutSeconds">-t, Per-request timeout in seconds (1-3600).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Command("set")]
    public async Task<int> Set(
        string? baseUrl = null,
        int? timeoutSeconds = null,
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
            new SetConfigurationCommand(parsedBaseUrl, timeoutSeconds),
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
        Console.WriteLine($"File                    {configuration.Location}");
        Console.WriteLine(
            $"                        {(configuration.Exists ? "present" : "not created yet — all values are defaults")}");
        Console.WriteLine();
        Console.WriteLine(
            $"ShareApi:BaseUrl        {configuration.BaseUrl}{Suffix(configuration.BaseUrlIsDefault)}");
        Console.WriteLine(
            $"ShareApi:TimeoutSeconds {configuration.TimeoutSeconds.ToString(CultureInfo.InvariantCulture)}{Suffix(configuration.TimeoutSecondsIsDefault)}");
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
