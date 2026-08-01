using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;
using Share.Api.Types;
using Share.Application.Abstractions.Api;
using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.FileSystem;
using Share.Application.Abstractions.Storage;
using Share.Infrastructure.Api;
using Share.Infrastructure.Configuration;
using Share.Infrastructure.FileSystem;
using Share.Infrastructure.Options;
using Share.Infrastructure.Storage;
using Share.Infrastructure.Time;
using SharedKernel;

namespace Share.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddConfigurationOptions(configuration)
            .AddServices()
            .AddShareApi();

    private static IServiceCollection AddConfigurationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ShareApiOptions>(configuration.GetSection(ShareApiOptions.SectionName));

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddSingleton<IConfigurationStore, YamlConfigurationStore>();

        services.AddSingleton<IFileScanner, FileScanner>();

        // A client of its own, without ApiKeyHeaderHandler: presigned URLs point at object
        // storage, and the Share API key has no business being sent there. No timeout
        // either — an upload takes as long as the file is, and cancellation stops it.
        services
            .AddHttpClient<IFileUploader, PresignedFileUploader>(client =>
                client.Timeout = Timeout.InfiniteTimeSpan);

        return services;
    }

    private static IServiceCollection AddShareApi(this IServiceCollection services)
    {
        services.AddTransient<ApiKeyHeaderHandler>();

        // AddRefitGeneratedClient, not AddRefitClient: every method on the generated
        // interface is emitted inline by Refit's source generator, so binding to the
        // generated stub avoids pulling in the reflection-based request builder.
        services
            .AddRefitGeneratedClient<IApiv1>()
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                ShareApiOptions options = serviceProvider
                    .GetRequiredService<IOptions<ShareApiOptions>>()
                    .Value;

                client.BaseAddress = options.BaseUrl;
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddHttpMessageHandler<ApiKeyHeaderHandler>();

        services.AddScoped<IShareApiClient, ShareApiClient>();

        return services;
    }
}
