using System.Net;
using Share.Application.Abstractions.FileSystem;
using Share.Infrastructure.Storage;
using SharedKernel;
using Shouldly;
using Xunit;

namespace Share.Infrastructure.UnitTests.Storage;

/// <summary>
/// Stubs the socket with a recording <see cref="HttpMessageHandler"/> rather than with
/// <c>StubHttp</c>: there is no Refit interface here, and what matters is the exact request
/// that reaches a presigned URL — method, body and content type.
/// </summary>
public sealed class PresignedFileUploaderTests : IDisposable
{
    private static readonly Uri UploadUrl = new("https://storage.example/bucket/report.pdf?sig=abc");

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"share-cli-upload-{Guid.NewGuid():N}");

    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (IDisposable disposable in _disposables)
        {
            disposable.Dispose();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private LocalFile Given(string relativePath, string content, string? contentType)
    {
        Directory.CreateDirectory(_root);

        string path = Path.Combine(_root, relativePath);
        File.WriteAllText(path, content);

        return new LocalFile(relativePath, path, new FileInfo(path).Length, contentType);
    }

    private (PresignedFileUploader Uploader, RecordingHandler Handler) UploaderFor(
        HttpStatusCode status = HttpStatusCode.OK) =>
        UploaderFor(new RecordingHandler(status));

    private (PresignedFileUploader Uploader, RecordingHandler Handler) UploaderFor(
        RecordingHandler handler)
    {
        var client = new HttpClient(handler);

        _disposables.Add(handler);
        _disposables.Add(client);

        return (new PresignedFileUploader(client), handler);
    }

    [Fact]
    public async Task UploadAsync_Should_PutTheFileBytes_WithItsContentType()
    {
        LocalFile file = Given("report.txt", "the contents", "text/plain");
        (PresignedFileUploader uploader, RecordingHandler handler) = UploaderFor();

        Result result = await uploader.UploadAsync(
            UploadUrl,
            file,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        handler.Method.ShouldBe(HttpMethod.Put);
        handler.RequestUri.ShouldBe(UploadUrl);
        handler.Body.ShouldBe("the contents");
        handler.ContentType.ShouldBe("text/plain");
    }

    [Fact]
    public async Task UploadAsync_Should_FallBackToOctetStream_WhenTheTypeIsUnknown()
    {
        LocalFile file = Given("blob.unknownext", "bytes", contentType: null);
        (PresignedFileUploader uploader, RecordingHandler handler) = UploaderFor();

        Result result = await uploader.UploadAsync(
            UploadUrl,
            file,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        handler.ContentType.ShouldBe("application/octet-stream");
    }

    [Fact]
    public async Task UploadAsync_Should_ReportTheStatus_WhenStorageRejectsTheUrl()
    {
        LocalFile file = Given("report.txt", "the contents", "text/plain");
        (PresignedFileUploader uploader, _) = UploaderFor(HttpStatusCode.Forbidden);

        Result result = await uploader.UploadAsync(
            UploadUrl,
            file,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Share.UploadRejected");
        result.Error.Description.ShouldContain("403");
        result.Error.Description.ShouldContain("report.txt");
    }

    [Fact]
    public async Task UploadAsync_Should_Fail_WhenTheConnectionDies()
    {
        LocalFile file = Given("report.txt", "the contents", "text/plain");
        (PresignedFileUploader uploader, _) =
            UploaderFor(new RecordingHandler(new HttpRequestException("connection refused")));

        Result result = await uploader.UploadAsync(
            UploadUrl,
            file,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Share.UploadFailed");
        result.Error.Description.ShouldContain("connection refused");
    }

    [Fact]
    public async Task UploadAsync_Should_Fail_WhenTheFileHasGoneAway()
    {
        LocalFile file = Given("report.txt", "the contents", "text/plain");
        File.Delete(file.FullPath);
        (PresignedFileUploader uploader, RecordingHandler handler) = UploaderFor();

        Result result = await uploader.UploadAsync(
            UploadUrl,
            file,
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Share.FileUnreadable");
        handler.Method.ShouldBeNull();
    }

    [Fact]
    public async Task UploadAsync_Should_ReportTheBytesItSends_AsARunningTotal()
    {
        LocalFile file = Given("report.txt", "the contents", "text/plain");
        (PresignedFileUploader uploader, _) = UploaderFor();
        var reported = new List<long>();

        Result result = await uploader.UploadAsync(
            UploadUrl,
            file,
            new Recorder(reported),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        // The counts must never go backwards and must end on the file's length: a caller is
        // going to set a bar to them directly.
        reported.ShouldNotBeEmpty();
        reported.ShouldBeInOrder();
        reported[^1].ShouldBe(file.Size);
    }

    [Fact]
    public async Task UploadAsync_Should_StillDeclareContentLength_WhenReportingProgress()
    {
        // Counting the bytes must not cost the request its Content-Length. Storage signs a
        // presigned PUT for a request that declares its length; one that arrived chunked
        // instead would be rejected.
        LocalFile file = Given("report.txt", "the contents", "text/plain");
        (PresignedFileUploader uploader, RecordingHandler handler) = UploaderFor();

        Result result = await uploader.UploadAsync(
            UploadUrl,
            file,
            new Recorder([]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        handler.ContentLength.ShouldBe(file.Size);
        handler.Body.ShouldBe("the contents");
    }

    private sealed class Recorder(List<long> reported) : IProgress<long>
    {
        public void Report(long value) => reported.Add(value);
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        private readonly Exception? _throws;

        public RecordingHandler(Exception throws)
            : this(HttpStatusCode.OK) => _throws = throws;

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? Body { get; private set; }

        public string? ContentType { get; private set; }

        public long? ContentLength { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (_throws is not null)
            {
                throw _throws;
            }

            Method = request.Method;
            RequestUri = request.RequestUri;
            ContentType = request.Content?.Headers.ContentType?.ToString();
            ContentLength = request.Content?.Headers.ContentLength;

            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(status);
        }
    }
}
