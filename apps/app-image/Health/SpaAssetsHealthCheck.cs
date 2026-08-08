using AppImage.Host.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AppImage.Host.Health;

/// <summary>
/// Readiness check: the React build output this host is supposed to serve is actually there.
/// </summary>
internal sealed class SpaAssetsHealthCheck(ValidatedAppImageOptions options) : IHealthCheck
{
    public const string Name = "spa-assets";

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // The path is deployment configuration, not a secret, and it is the one thing an operator
        // needs in order to fix this. It is reported to the log, never to the caller.
        return Task.FromResult(options.Web.IndexExists
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"The SPA document '{options.Web.IndexPath}' is missing."));
    }
}
