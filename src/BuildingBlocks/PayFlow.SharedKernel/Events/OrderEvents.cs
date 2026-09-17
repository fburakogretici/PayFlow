using System.Text.Json.Serialization;

namespace PayFlow.SharedKernel.Events;

public sealed record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);

public sealed record OrderCreatedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public string CustomerEmail { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];

    [JsonConstructor]
    public OrderCreatedIntegrationEvent(
        Guid orderId,
        string customerEmail,
        decimal totalAmount,
        IReadOnlyList<OrderItemDto> items,
        Guid id,
        DateTime occurredOnUtc,
        string correlationId) : base(id, occurredOnUtc, correlationId)
    {
        OrderId = orderId;
        CustomerEmail = customerEmail;
        TotalAmount = totalAmount;
        Items = items;
    }

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

public sealed record PaymentCompletedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public string TransactionId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime PaidAtUtc { get; init; }

    [JsonConstructor]
    public PaymentCompletedIntegrationEvent(
        Guid orderId,
        string transactionId,
        decimal amount,
        DateTime paidAtUtc,
        Guid id,
        DateTime occurredOnUtc,
        string correlationId) : base(id, occurredOnUtc, correlationId)
    {
        OrderId = orderId;
        TransactionId = transactionId;
        Amount = amount;
        PaidAtUtc = paidAtUtc;
    }

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

public sealed record PaymentFailedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public decimal Amount { get; init; }

    [JsonConstructor]
    public PaymentFailedIntegrationEvent(
        Guid orderId,
        string reason,
        decimal amount,
        Guid id,
        DateTime occurredOnUtc,
        string correlationId) : base(id, occurredOnUtc, correlationId)
    {
        OrderId = orderId;
        Reason = reason;
        Amount = amount;
    }

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
