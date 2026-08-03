using SharedKernel;

namespace Infrastructure.DomainEvents;

public interface IDomainEventsDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
