using Domain.Users;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Users.Create;

internal sealed class UserRegisteredDomainEventHandler(ILogger<UserRegisteredDomainEventHandler> logger) : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("User {UserId} registered", domainEvent.UserId);
        return Task.CompletedTask;
    }
}
