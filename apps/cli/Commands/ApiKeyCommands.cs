using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.ApiKeys.Create;
using Application.ApiKeys.Get;
using Application.Users.Create;
using ConsoleAppFramework;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel;


namespace Cli.Commands;

public class ApiKeyCommands(ILogger<DatabaseCommands> logger, IServiceProvider serviceProvider)
{
    [Command("create-api-key")]
    public async Task CreateApiKey(string userId)
    {
        logger.LogInformation("Creating Api Key");
        using IServiceScope scope = serviceProvider.CreateScope();

        ICommandHandler<CreateApiKeyCommand, CreateApiKeyResponse> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateApiKeyCommand, CreateApiKeyResponse>>();

        var userIdGuid = Guid.Parse(userId);
        var command = new CreateApiKeyCommand(userIdGuid, "TestApiKey");
        Result<CreateApiKeyResponse> response = await handler.Handle(command, CancellationToken.None);
        logger.LogInformation("Api Key Created: {ApiKey}", response.Value.Key);
    }

    [Command("validate-api-key")]
    public async Task ValidateApiKey(string apiKey)
    {
        logger.LogInformation("Validating Api Key");
        using IServiceScope scope = serviceProvider.CreateScope();

        IQueryHandler<GetApiKeyQuery, ApiKeyResponse> handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<GetApiKeyQuery, ApiKeyResponse>>();
        var query = new GetApiKeyQuery(apiKey);
        Result<ApiKeyResponse> response = await handler.Handle(query, CancellationToken.None);

        logger.LogInformation("Api Key Verification Result: {Result}", response.IsSuccess);
    }
}
