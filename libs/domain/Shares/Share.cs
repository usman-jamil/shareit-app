using SharedKernel;

namespace Domain.Shares;

public class Share : Entity
{
    public Guid OwnerUserId { get; set; }

    public string Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public int ConfiguredTtlMinutes { get; set; }

    public long TotalBytes { get; set; }

    public int FileCount { get; set; }
}
