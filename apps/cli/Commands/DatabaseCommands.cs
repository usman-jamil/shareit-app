using ConsoleAppFramework;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace Cli.Commands;

public class DatabaseCommands(ILogger<DatabaseCommands> logger, IServiceProvider serviceProvider)
{
    [Command("apply-migration")]
    public async Task ApplyMigrations()
    {
        logger.LogInformation("Applying migrations");
        using IServiceScope scope = serviceProvider.CreateScope();
        await using ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    [Command("drop-database")]
    public async Task DropDatabase()
    {
        logger.LogInformation("Dropping database");
        using IServiceScope scope = serviceProvider.CreateScope();
        await using ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
    }
}
