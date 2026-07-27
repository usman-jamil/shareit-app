using Microsoft.Extensions.Options;
using Share.Infrastructure.Options;

namespace Share.Infrastructure.Api;

/// <summary>
/// Attaches the configured API key to every outgoing request. The API authorizes on the
/// <c>X-Api-Key</c> header; keeping it here means no call site has to remember it.
/// </summary>
internal sealed class ApiKeyHeaderHandler(IOptions<ShareApiOptions> options) : DelegatingHandler
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string apiKey = options.Value.ApiKey;

        // An absent key is not an error here — let the API answer with 401 so it maps to a
        // failure result like any other API-reported failure.
        if (!string.IsNullOrWhiteSpace(apiKey) && !request.Headers.Contains(ApiKeyHeaderName))
        {
            request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
