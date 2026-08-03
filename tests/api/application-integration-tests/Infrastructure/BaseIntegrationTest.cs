using Application.Abstractions.Messaging;
using Application.Shares.Create;
using Application.Shares.GetById;
using Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Application.IntegrationTests.Infrastructure;

public abstract class BaseIntegrationTest : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IServiceScope _scope;
    protected readonly IQueryHandler<GetShareByIdQuery, ShareResponse> GetShareHandler;
    protected readonly ICommandHandler<CreateShareCommand, CreateShareResponse> CreateShareHandler;
    protected readonly ApplicationDbContext DbContext;

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        _scope = factory.Services.CreateScope();

        GetShareHandler = _scope.ServiceProvider.GetRequiredService<IQueryHandler<GetShareByIdQuery, ShareResponse>>();
        CreateShareHandler = _scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateShareCommand, CreateShareResponse>>();
        DbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
