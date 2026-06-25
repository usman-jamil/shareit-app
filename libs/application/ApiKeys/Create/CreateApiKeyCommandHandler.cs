using System.Security.Cryptography;
using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Domain.ApiKeys;
using Domain.Users;
using SharedKernel;

namespace Application.ApiKeys.Create;

internal sealed class CreateApiKeyCommandHandler(
    IApiKeyRepository apiKeyRepository,
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider,
    IApiKeyHasher apiKeyHasher,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateApiKeyCommand, CreateApiKeyResponse>
{
    public async Task<Result<CreateApiKeyResponse>> Handle(
        CreateApiKeyCommand command,
        CancellationToken cancellationToken)
    {
        User? user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<CreateApiKeyResponse>(UserErrors.NotFound(command.UserId));
        }
        
        // Public, non-secret lookup id. Indexed, unique, stored in plaintext.
        string keyId = apiKeyHasher.KeyId;

        // Secret: 256 bits. Returned to the caller once, never stored.
        byte[] secretBytes = RandomNumberGenerator.GetBytes(32);
        string secret = Convert.ToHexString(secretBytes);
        
        // Full key the caller sends: share_<keyId>_<secret>
        string plaintextKey = $"{apiKeyHasher.KeyPrefix}_{keyId}_{secret}";
        
        // Hash ONLY the secret. Lookup is by keyId, not by hash.
        string keyHash = apiKeyHasher.Hash(secret);

        var apiKey = new ApiKey
        {
            UserId = user.Id,
            KeyId = keyId,
            KeyHash = keyHash,
            Prefix = apiKeyHasher.KeyPrefix,
            Label = command.Label,
            CreatedAt = dateTimeProvider.UtcNow,
            LastUsedAt = null,
            RevokedAt = null
        };

        apiKeyRepository.Add(apiKey);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateApiKeyResponse
        {
            Id = apiKey.Id,
            UserId = apiKey.UserId,
            KeyId = apiKey.KeyId,
            Prefix = apiKey.Prefix,
            Label = apiKey.Label,
            CreatedAt = apiKey.CreatedAt,
            Key = plaintextKey
        };
    }
}
