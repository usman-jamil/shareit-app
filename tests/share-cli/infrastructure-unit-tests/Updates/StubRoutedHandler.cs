using System.Net;

namespace Share.Infrastructure.UnitTests.Updates;

/// <summary>
/// Answers by absolute URL. <c>StubHttp</c> is not used here because none of these callers
/// go through Refit — what is being exercised is a plain <see cref="HttpClient"/> against
/// GitHub and its asset host, so the socket is what gets stubbed.
/// </summary>
internal sealed class StubRoutedHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes =
        new(StringComparer.Ordinal);

    public List<Uri> Requests { get; } = [];

    public StubRoutedHandler Json(string url, string body) =>
        Respond(url, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });

    public StubRoutedHandler Text(string url, string body) =>
        Respond(url, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        });

    public StubRoutedHandler Bytes(string url, byte[] body) =>
        Respond(url, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body)
        });

    public StubRoutedHandler Status(string url, HttpStatusCode status) =>
        Respond(url, () => new HttpResponseMessage(status));

    public StubRoutedHandler Throws(string url, Exception exception) =>
        Respond(url, () => throw exception);

    public StubRoutedHandler Respond(string url, Func<HttpResponseMessage> response)
    {
        _routes[url] = response;

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Uri uri = request.RequestUri!;

        Requests.Add(uri);

        await Task.Yield();

        if (_routes.TryGetValue(uri.ToString(), out Func<HttpResponseMessage>? response))
        {
            return response();
        }

        // An unrouted URL is a test that asked for something it did not arrange; 404 says
        // so more usefully than a NullReferenceException would.
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
