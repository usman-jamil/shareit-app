using Application.Abstractions.Messaging;
using Application.Users.Create;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Cli.Commands;

public class UserCommands(ILogger<UserCommands> logger, IServiceProvider serviceProvider)
{
    [Command("create-user")]
    public async Task CreateUser(string name, string email)
    {
        logger.LogInformation("Creating User");
        using IServiceScope scope = serviceProvider.CreateScope();
        ICommandHandler<CreateUserCommand, Guid> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateUserCommand, Guid>>();
        var userCommand = new CreateUserCommand(
            name,
            email);
        
        Result<Guid> result = await handler.Handle(userCommand, CancellationToken.None);
        logger.LogInformation("User Created: {Result}", result.Value.ToString());
    }
}
