using System.Net.Http.Headers;
using Share.Application.Abstractions.FileSystem;
using Share.Application.Abstractions.Storage;
using Share.Domain.Shares;
using SharedKernel;

namespace Share.Infrastructure.Storage;

/// <summary>
/// PUTs file bytes straight to object storage using the presigned URLs the API issued.
/// </summary>
/// <remarks>
/// Uses its own <see cref="HttpClient"/>, registered without
/// <see cref="Api.ApiKeyHeaderHandler"/>: these requests go to storage, not to the Share
/// API, and the API key must not travel there. The client is given no timeout — an upload
/// is as long as the file is, so cancellation (Ctrl-C) is what stops it.
/// </remarks>
internal sealed class PresignedFileUploader(HttpClient httpClient) : IFileUploader
{
    private static readonly MediaTypeHeaderValue FallbackContentType = new("application/octet-stream");

    public async Task<Result> UploadAsync(
        Uri uploadUrl,
        LocalFile file,
        IProgress<long>? bytesUploaded = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        try
        {
            await using FileStream source = File.OpenRead(file.FullPath);

            // Counting happens on the way out of the file rather than being estimated from
            // the file size, so what is reported is what has actually been handed to the
            // socket. Unwrapped when nobody is watching — an untracked upload should not pay
            // for a callback per read.
            Stream stream = bytesUploaded is null
                ? source
                : new ProgressReportingStream(source, bytesUploaded);

            using var content = new StreamContent(stream);

            content.Headers.ContentType = ContentTypeFor(file);

            using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = content };

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

            return response.IsSuccessStatusCode
                ? Result.Success()
                : Result.Failure(
                    ShareErrors.UploadRejected(file.RelativePath, (int)response.StatusCode));
        }
        catch (HttpRequestException exception)
        {
            return Result.Failure(ShareErrors.UploadFailed(file.RelativePath, exception.Message));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The caller's token is still live, so this was the HttpClient's own timeout
            // rather than the user cancelling.
            return Result.Failure(ShareErrors.UploadTimedOut(file.RelativePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Opening the file, or reading it part-way through the upload: HttpClient
            // surfaces network trouble as HttpRequestException, so what lands here is the
            // local file rather than the connection.
            return Result.Failure(ShareErrors.FileUnreadable(file.RelativePath, exception.Message));
        }
    }

    private static MediaTypeHeaderValue ContentTypeFor(LocalFile file) =>
        file.ContentType is not null &&
        MediaTypeHeaderValue.TryParse(file.ContentType, out MediaTypeHeaderValue? contentType)
            ? contentType
            : FallbackContentType;
}
