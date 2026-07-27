using System.Net;
using Refit.Testing;
using Share.Infrastructure.Api;
using Share.Infrastructure.Options;
using Shouldly;
using Xunit;

// Share.Infrastructure.Options shadows the Microsoft.Extensions.Options namespace here.
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Share.Infrastructure.UnitTests.Api;

public class ApiKeyHeaderHandlerTests
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string RequestUri = "http://localhost:5080/users/1";

    private static async Task<HttpRequestMessage> SendAsync(StubHttp http, string apiKey)
    {
        using var handler = new ApiKeyHeaderHandler(
            OptionsFactory.Create(new ShareApiOptions { ApiKey = apiKey }))
        {
            InnerHandler = http
        };

        using var client = new HttpClient(handler, disposeHandler: false);
        using HttpResponseMessage response = await client.GetAsync(
            new Uri(RequestUri),
            TestContext.Current.CancellationToken);

        return http.Requests[^1];
    }

    [Fact]
    public async Task SendAsync_Should_AttachTheConfiguredApiKey()
    {
        using var http = new StubHttp
        {
            { Route.Get("/users/{userId}"), Reply.Status(HttpStatusCode.OK) }
        };

        HttpRequestMessage request = await SendAsync(http, "secret-key");

        request.Headers.GetValues(ApiKeyHeaderName).ShouldBe(["secret-key"]);
    }

    [Fact]
    public async Task SendAsync_Should_SendNoHeader_WhenNoApiKeyIsConfigured()
    {
        using var http = new StubHttp
        {
            { Route.Get("/users/{userId}"), Reply.Status(HttpStatusCode.OK) }
        };

        // An absent key is deliberately not an error here: the API answers 401 and the
        // adapter turns that into a normal failure result.
        HttpRequestMessage request = await SendAsync(http, string.Empty);

        request.Headers.Contains(ApiKeyHeaderName).ShouldBeFalse();
    }
}
