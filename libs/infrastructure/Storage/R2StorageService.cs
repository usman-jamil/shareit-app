using Amazon.S3;
using Amazon.S3.Model;
using Application.Abstractions.Data;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

public sealed class R2StorageService(IAmazonS3 s3Client, IOptions<StorageOptions> options) : IStorageService
{
    private readonly string _bucket = options.Value.BucketName;

    public async Task DeleteFileAsync(string key, CancellationToken ct = default)
    {
        await s3Client.DeleteObjectAsync(_bucket, key, ct);
    }

    public async Task<string> GeneratePresignedUploadUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default)
    {
        var presign = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.Now.AddDays(7),
        };

        return await s3Client.GetPreSignedURLAsync(presign);
    }

    public async Task<string> GeneratePresignedDownloadUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default)
    {
        var presign = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.Now.AddDays(7),
        };

        return await s3Client.GetPreSignedURLAsync(presign);
    }
}
