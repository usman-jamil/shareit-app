using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Authorization;

public class ApiKeyRequirement(string policyName) : IAuthorizationRequirement
{
    public string PolicyName { get; } = policyName;
}
