using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Share.Application.Abstractions.Messaging;
using Share.Application.Ping;
using SharedKernel;

namespace Share.Cli.Commands;

public class PingCommands(ILogger<PingCommands> logger, IServiceProvider serviceProvider)
{
    /// <summary>
    /// Placeholder command that verifies the host, DI and CQRS pipeline are wired up.
    /// </summary>
    [Command("ping")]
    public async Task Ping(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        IQueryHandler<PingQuery, string> handler =
            scope.ServiceProvider.GetRequiredService<IQueryHandler<PingQuery, string>>();

        Result<string> result = await handler.Handle(new PingQuery(), cancellationToken);

        logger.LogInformation("{Result}", result.Value);
    }
}
