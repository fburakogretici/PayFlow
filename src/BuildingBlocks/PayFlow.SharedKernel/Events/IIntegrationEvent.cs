namespace PayFlow.SharedKernel.Events;

public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTime OccurredOnUtc { get; }
    string CorrelationId { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");

    protected IntegrationEvent()
    {
    }

    protected IntegrationEvent(Guid id, DateTime occurredOnUtc, string correlationId)
    {
        Id = id;
        OccurredOnUtc = occurredOnUtc;
        CorrelationId = correlationId;
    }
}
