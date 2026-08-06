using AppImage.Host.Configuration;
using AppImage.Host.Health;
using AppImage.Host.Proxy;
using AppImage.Host.Spa;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Yarp.ReverseProxy.Configuration;

// Container HEALTHCHECK path. Handled before anything else is built so the probe never binds a port.
if (HealthProbe.IsProbeRequested(args))
{
    return await HealthProbe.RunAsync(CancellationToken.None);
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(EnvironmentConfiguration.Read(Environment.GetEnvironmentVariables()));

AppImageOptions configured =
    builder.Configuration.GetSection(AppImageOptions.SectionName).Get<AppImageOptions>() ?? new AppImageOptions();

// Startup validation. A misconfigured web root or destination would otherwise show up as a 404 or
// a 502 on the first real request, long after the deploy that caused it.
if (!AppImageOptionsValidator.TryValidate(configured, out ValidatedAppImageOptions? options, out IReadOnlyList<string> failures))
{
    await Console.Error.WriteLineAsync("The app image host is misconfigured and will not start:");
    foreach (string failure in failures)
    {
        await Console.Error.WriteLineAsync($"  - {failure}");
    }

    return 78; // EX_CONFIG
}

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<SpaFallbackHandler>();

builder.Services
    .AddReverseProxy()
    .LoadFromMemory(ProxyConfiguration.CreateRoutes(options), ProxyConfiguration.CreateClusters(options));

builder.Services
    .AddHttpClient(ApiReachabilityHealthCheck.HttpClientName, client =>
    {
        client.BaseAddress = options.ApiDestination;
        client.Timeout = options.ApiHealthTimeout;
    });

builder.Services
    .AddHealthChecks()
    .AddCheck<SpaAssetsHealthCheck>(SpaAssetsHealthCheck.Name, tags: [HealthTags.Ready])
    .AddCheck<ApiReachabilityHealthCheck>(ApiReachabilityHealthCheck.Name, tags: [HealthTags.Ready]);

builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
{
    forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

    if (options.TrustAllProxies)
    {
        // Only correct when nothing untrusted can reach this port directly.
        forwarded.KnownIPNetworks.Clear();
        forwarded.KnownProxies.Clear();
    }
});

WebApplication app = builder.Build();

app.Logger.LogInformation(
    "Serving {WebRoot}; proxying {PathPrefix} to {Destination} (prefix stripped: {StripPrefix}).",
    options.Web.RootPath,
    options.ApiPathPrefix,
    options.ApiDestination,
    options.StripApiPathPrefix);

// 1. Infrastructure middleware.
app.UseForwardedHeaders();

// Explicit, so the ordering below is the ordering in the pipeline rather than one WebApplication
// infers by inserting UseRouting ahead of the first middleware.
app.UseRouting();

// 2. This host's own health endpoints. Never proxied — they answer for the container, not the API.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    // Liveness: the process is up and serving. Dependency state belongs to readiness.
    Predicate = _ => false,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthTags.Ready),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
    // The default writer emits the status word only. Failure descriptions stay in the logs.
});

// 3. The API proxy, mapped ahead of the static files and the SPA fallback so that /api is answered
//    by the API — including its 404s for routes it does not have.
app.MapReverseProxy();

// 4. The built React application.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = options.Web.FileProvider,
    ServeUnknownFileTypes = false,
    OnPrepareResponse = SpaCacheHeaders.Apply,
    ContentTypeProvider = new FileExtensionContentTypeProvider(),
});

// 5. Client-side routes, and 6. the guard that keeps /api out of the SPA document.
app.MapFallback(context =>
    context.RequestServices.GetRequiredService<SpaFallbackHandler>().HandleAsync(context));

await app.RunAsync();

return 0;
