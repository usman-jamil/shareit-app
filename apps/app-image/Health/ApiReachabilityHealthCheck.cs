using AppImage.Host.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AppImage.Host.Health;

/// <summary>
/// Readiness check: the API process inside this container answers HTTP.
/// </summary>
/// <remarks>
/// The distinction this check draws is deliberate. <em>Unhealthy</em> means the API did not answer
/// at all — the process is down or not listening, and every <c>/api</c> request will fail, so this
/// container is not ready. <em>Degraded</em> means the API answered but reported itself unwell:
/// routing works and the container can serve traffic, and the API's own dependency health is its
/// own to report (it is reachable at <c>/api/health</c>). Degraded still returns 200 from
/// <c>/health/ready</c>, so a database outage does not take the SPA offline as well.
/// </remarks>
internal sealed class ApiReachabilityHealthCheck(
    IHttpClientFactory httpClientFactory,
    ValidatedAppImageOptions options) : IHealthCheck
{
    public const string Name = "internal-api";
    public const string HttpClientName = "internal-api-health";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using HttpResponseMessage response = await client.GetAsync(
                options.ApiHealthPath,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded(
                    $"The internal API answered {(int)response.StatusCode} from '{options.ApiHealthPath}'.");
        }
        catch (HttpRequestException exception)
        {
            return HealthCheckResult.Unhealthy("The internal API is not reachable.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient surfaces its own timeout as a cancellation; the caller's token is separate.
            return HealthCheckResult.Unhealthy(
                $"The internal API did not answer within {options.ApiHealthTimeout.TotalSeconds:0} seconds.",
                exception);
        }
    }
}
