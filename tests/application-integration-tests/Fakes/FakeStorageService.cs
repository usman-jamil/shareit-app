using Application.Abstractions.Data;

namespace Application.IntegrationTests.Fakes;

/// <summary>
/// In-memory stand-in for the real R2/S3 storage service. Presigned URL
/// generation is deterministic and does not require credentials or network
/// access, keeping integration tests hermetic.
/// </summary>
public sealed class FakeStorageService : IStorageService
{
    public Task DeleteFileAsync(string key, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<string> GeneratePresignedUploadUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default) =>
        Task.FromResult($"https://fake-storage.test/upload/{key}");

    public Task<string> GeneratePresignedDownloadUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default) =>
        Task.FromResult($"https://fake-storage.test/download/{key}");
}
