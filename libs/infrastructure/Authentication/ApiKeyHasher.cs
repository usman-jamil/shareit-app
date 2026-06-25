using System.Security.Cryptography;
using Application.Abstractions.Authentication;
using System.Text;


namespace Infrastructure.Authentication;

internal sealed class ApiKeyHasher(byte[] pepper) : IApiKeyHasher
{
    public string Hash(string secret)
    {
        using var hmac = new HMACSHA256(pepper);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hash);
    }

    public bool Verify(string secret, string storedHash)
    {
        byte[] expected = Convert.FromHexString(storedHash);

        using var hmac = new HMACSHA256(pepper);
        byte[] actual = hmac.ComputeHash(Encoding.UTF8.GetBytes(secret));

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
    
    public string KeyId =>
        // 8 random bytes -> 16 hex chars. Collision-resistant; hex keeps it delimiter-safe.
        Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

    public string KeyPrefix => "share";
}
