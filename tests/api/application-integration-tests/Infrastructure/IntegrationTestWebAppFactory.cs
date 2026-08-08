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
        string connectionString = $"{_dbContainer.GetConnectionString()};Pooling=False";

        // The host reads these at startup, before ConfigureTestServices can swap
        // anything out. On a developer machine they come from user secrets, which
        // do not exist on CI — supply them here so the app boots identically
        // everywhere. The real values are irrelevant: the database points at the
        // container below, and the services that consume the rest are faked.
        builder.UseSetting("ConnectionStrings:Database", connectionString);
        builder.UseSetting("ApiKey:Pepper", Convert.ToBase64String("integration-tests"u8.ToArray()));
        builder.UseSetting("Storage:AccessKeyId", "integration-tests");
        builder.UseSetting("Storage:SecretAccessKey", "integration-tests");
        builder.UseSetting("Storage:ServiceUrl", "http://localhost");
        builder.UseSetting("Storage:BucketName", "integration-tests");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));

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
