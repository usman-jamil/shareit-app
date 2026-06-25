namespace Application.Abstractions.Authentication;

public interface IApiKeyHasher
{
    string Hash(string secret);

    bool Verify(string secret, string storedHash);
    
    string KeyId { get; }
    
    string KeyPrefix { get; }
}
