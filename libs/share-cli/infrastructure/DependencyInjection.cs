using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;
using Share.Api.Types;
using Share.Application.Abstractions.Api;
using Share.Application.Abstractions.Configuration;
using Share.Application.Abstractions.FileSystem;
using Share.Application.Abstractions.Storage;
using Share.Application.Abstractions.Updates;
using Share.Infrastructure.Api;
using Share.Infrastructure.Configuration;
using Share.Infrastructure.FileSystem;
using Share.Infrastructure.Options;
using Share.Infrastructure.Storage;
using Share.Infrastructure.Time;
using Share.Infrastructure.Updates;
using SharedKernel;

namespace Share.Infrastructure;

public static class DependencyInjection
{
    private const string GitHubMediaType = "application/vnd.github+json";
    private const string GitHubApiVersionHeader = "X-GitHub-Api-Version";
    private const string GitHubApiVersion = "2022-11-28";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddConfigurationOptions(configuration)
            .AddServices()
            .AddShareApi()
            .AddSelfUpdate();

    private static IServiceCollection AddConfigurationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ShareApiOptions>(configuration.GetSection(ShareApiOptions.SectionName));
        services.Configure<UpdateOptions>(configuration.GetSection(UpdateOptions.SectionName));

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

    /// <summary>
    /// Wires <c>share update</c>. Both clients here talk to GitHub, so neither carries
    /// <see cref="ApiKeyHeaderHandler"/> — the Share API key must not be sent there.
    /// </summary>
    private static IServiceCollection AddSelfUpdate(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationEnvironment, ApplicationEnvironment>();
        services.AddSingleton<IUpdateProcessLauncher, UpdateProcessLauncher>();

        services
            .AddHttpClient<IReleaseCatalog, GitHubReleaseCatalog>((serviceProvider, client) =>
            {
                UpdateOptions options = serviceProvider
                    .GetRequiredService<IOptions<UpdateOptions>>()
                    .Value;

                client.BaseAddress = options.ApiBaseUrl;
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

                ConfigureGitHubHeaders(serviceProvider, client);
                client.DefaultRequestHeaders.Accept.ParseAdd(GitHubMediaType);
                client.DefaultRequestHeaders.Add(GitHubApiVersionHeader, GitHubApiVersion);
            });

        // No timeout: a release archive is ~35 MB and takes as long as the connection takes.
        // Cancellation is what stops it, exactly as with an upload.
        services
            .AddHttpClient<IUpdatePackageInstaller, UpdatePackageInstaller>(
                (serviceProvider, client) =>
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;

                    ConfigureGitHubHeaders(serviceProvider, client);
                });

        return services;
    }

    /// <summary>
    /// GitHub rejects requests without a User-Agent, and asks that it identify the caller.
    /// </summary>
    private static void ConfigureGitHubHeaders(IServiceProvider serviceProvider, HttpClient client)
    {
        IApplicationEnvironment environment = serviceProvider
            .GetRequiredService<IApplicationEnvironment>();

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"share-cli/{environment.CurrentVersion?.ToString() ?? "0.0.0"}");
    }
}
