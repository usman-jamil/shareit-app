using SharedKernel;

namespace Domain.Shares;

public sealed class Share : Entity
{
    public Share()
    {

    }

    public Guid OwnerUserId { get; set; }

    public string Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public int ConfiguredTtlMinutes { get; set; }

    public long TotalBytes { get; set; }

    public int FileCount { get; set; }

    public Share(Guid id, Guid ownerUserId, string status, DateTime createdAt, DateTime updatedAt, DateTime expiresAt, int configuredTtlMinutes, long totalBytes, int fileCount) : base(id)
    {
        OwnerUserId = ownerUserId;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ExpiresAt = expiresAt;
        ConfiguredTtlMinutes = configuredTtlMinutes;
        TotalBytes = totalBytes;
        FileCount = fileCount;
    }
}
