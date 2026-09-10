namespace PayFlow.SharedKernel.Events;

public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTime OccurredOnUtc { get; }
    string CorrelationId { get; }
}

public abstract record IntegrationEvent(Guid Id, DateTime OccurredOnUtc, string CorrelationId) : IIntegrationEvent
{
    protected IntegrationEvent() : this(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid().ToString("N"))
    {
    }
}
