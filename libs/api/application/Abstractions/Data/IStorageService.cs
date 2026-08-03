namespace Application.Abstractions.Data;

public interface IStorageService
{
    Task DeleteFileAsync(string key, CancellationToken ct = default);
    Task<string> GeneratePresignedUploadUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default);
    Task<string> GeneratePresignedDownloadUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default);
}
