using Amazon.Runtime;
using Amazon.S3;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.ApiKeys;
using Domain.Files;
using Domain.Shares;
using Domain.Users;
using Infrastructure.Authentication;
using Infrastructure.Authorization;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Infrastructure.Repositories;
using Infrastructure.Storage;
using Infrastructure.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddServices()
            .AddPersistence(configuration)
            .AddHealthChecks(configuration)
            .AddStorage(configuration)
            .AddAuthenticationInternal(configuration)
            .AddAuthorizationInternal();

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database") ??
                                   throw new ArgumentNullException(nameof(configuration));

        services.AddDbContext<ApplicationDbContext>(
            options => options
                .UseNpgsql(connectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IFileRepository, FileRepository>();

        services.AddScoped<IShareRepository, ShareRepository>();

        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddSingleton<ISqlConnectionFactory>(_ =>
            new SqlConnectionFactory(connectionString));

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Database")!);

        return services;
    }

    private static IServiceCollection AddStorage(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IAmazonS3>(sp =>
        {
            StorageOptions opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new AmazonS3Client(
                new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey),
                new AmazonS3Config
                {
                    ServiceURL = opts.ServiceUrl
                });
        });
        // Store Storage:AccessKeyId in user secrets
        // Store Storage:SecretAccessKey in user secrets
        // Store Storage:ServiceUrl in user secrets
        services.AddSingleton<IStorageService, R2StorageService>();

        return services;
    }

    private static IServiceCollection AddAuthenticationInternal(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));

        services.AddHttpContextAccessor();
        // Store ApiKey:Pepper in user secrets
        services.AddSingleton<IApiKeyHasher>(sp =>
        {
            ApiKeyOptions opts = sp.GetRequiredService<IOptions<ApiKeyOptions>>().Value;
            byte[] pepper = Convert.FromBase64String(opts.Pepper);
            return new ApiKeyHasher(pepper);
        });
        services.AddScoped<IUserContext, UserContext>();

        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddTransient<IAuthorizationHandler, ApiKeyAuthorizationHandler>();
        services.AddTransient<IAuthorizationPolicyProvider, ApiKeyAuthorizationPolicyProvider>();

        return services;
    }
}
