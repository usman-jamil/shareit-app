using SharedKernel;

namespace Domain.Files;

public class File : Entity
{
    public Guid ShareId { get; set; }

    public string RelativePath { get; set; }

    public string Sha256 { get; set; }

    public string? ContentType { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Size { get; set; }
}
