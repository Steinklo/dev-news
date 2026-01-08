using Mediator;

namespace DevNews.Domain.Common;

public class DomainEvent : INotification
{
    protected DomainEvent(Guid aggregateId)
    {
        AggregateId = aggregateId;
    }

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid AggregateId { get; protected set; }
    public DateTime Created { get; init; } = DateTime.UtcNow;
}