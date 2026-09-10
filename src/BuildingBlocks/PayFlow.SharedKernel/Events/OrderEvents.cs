namespace PayFlow.SharedKernel.Events;

public sealed record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);

public sealed record OrderCreatedIntegrationEvent(
    Guid OrderId,
    string CustomerEmail,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items,
    Guid Id,
    DateTime OccurredOnUtc,
    string CorrelationId) : IntegrationEvent(Id, OccurredOnUtc, CorrelationId)
{
    public OrderCreatedIntegrationEvent(
        Guid orderId,
        string customerEmail,
        decimal totalAmount,
        IReadOnlyList<OrderItemDto> items,
        string? correlationId = null)
        : this(
            orderId,
            customerEmail,
            totalAmount,
            items,
            Guid.NewGuid(),
            DateTime.UtcNow,
            correlationId ?? Guid.NewGuid().ToString("N"))
    {
    }
}

public sealed record PaymentCompletedIntegrationEvent(
    Guid OrderId,
    string TransactionId,
    decimal Amount,
    DateTime PaidAtUtc,
    Guid Id,
    DateTime OccurredOnUtc,
    string CorrelationId) : IntegrationEvent(Id, OccurredOnUtc, CorrelationId)
{
    public PaymentCompletedIntegrationEvent(
        Guid orderId,
        string transactionId,
        decimal amount,
        string? correlationId = null)
        : this(
            orderId,
            transactionId,
            amount,
            DateTime.UtcNow,
            Guid.NewGuid(),
            DateTime.UtcNow,
            correlationId ?? Guid.NewGuid().ToString("N"))
    {
    }
}

public sealed record PaymentFailedIntegrationEvent(
    Guid OrderId,
    string Reason,
    decimal Amount,
    Guid Id,
    DateTime OccurredOnUtc,
    string CorrelationId) : IntegrationEvent(Id, OccurredOnUtc, CorrelationId)
{
    public PaymentFailedIntegrationEvent(
        Guid orderId,
        string reason,
        decimal amount,
        string? correlationId = null)
        : this(
            orderId,
            reason,
            amount,
            Guid.NewGuid(),
            DateTime.UtcNow,
            correlationId ?? Guid.NewGuid().ToString("N"))
    {
    }
}
