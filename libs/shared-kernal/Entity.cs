namespace SharedKernel;

public abstract class Entity(Guid id)
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public List<IDomainEvent> DomainEvents => [.. _domainEvents];
    
    protected Entity() : this(Guid.NewGuid())
    {
    }

    public Guid Id { get; init; } = id;

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}
