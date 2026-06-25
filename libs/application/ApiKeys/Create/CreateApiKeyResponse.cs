namespace Application.ApiKeys.Create;

public sealed class CreateApiKeyResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    
    public string KeyId { get; set; }

    public string Prefix { get; set; }

    public string? Label { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The plaintext API key. Only ever returned once, at creation time — it is not persisted.
    /// </summary>
    public string Key { get; set; }
}
