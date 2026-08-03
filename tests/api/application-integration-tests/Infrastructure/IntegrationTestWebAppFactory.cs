using Application.Abstractions.Data;
using Application.IntegrationTests.Fakes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.Seq;
using Xunit;

namespace Application.IntegrationTests.Infrastructure;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:18")
        .WithEnvironment("POSTGRES_DB", "share")
        .WithEnvironment("POSTGRES_USER", "postgres")
        .WithEnvironment("POSTGRES_PASSWORD", "postgres")
        .Build();

    private readonly SeqContainer _seqContainer = new SeqBuilder("datalust/seq:latest")
        .WithEnvironment("ACCEPT_EULA", "Y")
        .WithEnvironment("SEQ_FIRSTRUN_NOAUTHENTICATION", "true")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));

            string connectionString = $"{_dbContainer.GetConnectionString()};Pooling=False";

            services.AddDbContext<ApplicationDbContext>(options =>
                options
                    .UseNpgsql(connectionString)
                    .UseSnakeCaseNamingConvention());

            services.RemoveAll<ISqlConnectionFactory>();

            services.AddSingleton<ISqlConnectionFactory>(_ =>
                new SqlConnectionFactory(connectionString));

            // The real R2/S3 storage service needs credentials that only live in
            // user secrets. Swap in a fake so use cases that issue presigned URLs
            // (e.g. creating a share) run hermetically.
            services.RemoveAll<IStorageService>();

            services.AddSingleton<IStorageService, FakeStorageService>();
        });
    }

    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _seqContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await _seqContainer.StopAsync();
    }
}
