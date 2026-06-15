namespace Application.ApiKeys.GetById;

public class ApiKeyResponse
{
  public Guid Id { get; set; }

  public Guid UserId { get; set; }

  public string KeyHash { get; set; }

  public string Prefix { get; set; }

  public string? Label { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime? LastUsedAt { get; set; }

  public DateTime? RevokedAt { get; set; }
}
