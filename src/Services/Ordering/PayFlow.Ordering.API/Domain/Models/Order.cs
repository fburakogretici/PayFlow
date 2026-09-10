using PayFlow.Ordering.API.Domain.Enums;
using PayFlow.SharedKernel.Results;

namespace PayFlow.Ordering.API.Domain.Models;

public class Order
{
    private readonly List<OrderItem> _items = [];

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;
    public Address ShippingAddress { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public decimal TotalAmount => _items.Sum(i => i.TotalPrice);
    public string? PaymentTransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private Order() { } // EF Core için

    public static Result<Order> Create(
        Guid customerId,
        string customerEmail,
        Address shippingAddress,
        IEnumerable<OrderItem> items)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
            return Result.Failure<Order>(Error.Validation("Order.EmailRequired", "Customer email is required."));

        var itemsList = items.ToList();
        if (itemsList.Count == 0)
            return Result.Failure<Order>(Error.Validation("Order.EmptyItems", "An order must contain at least one item."));

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            ShippingAddress = shippingAddress,
            Status = OrderStatus.PendingPayment,
            CreatedAtUtc = DateTime.UtcNow
        };

        order._items.AddRange(itemsList);

        return Result.Success(order);
    }

    public Result MarkAsPaid(string transactionId)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            return Result.Failure(Error.Conflict(
                "Order.InvalidState",
                $"Order cannot be marked as paid when status is {Status}."));
        }

        Status = OrderStatus.Paid;
        PaymentTransactionId = transactionId;
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }

    public Result MarkAsPaymentFailed(string reason)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            return Result.Failure(Error.Conflict(
                "Order.InvalidState",
                $"Order cannot be failed when status is {Status}."));
        }

        Status = OrderStatus.PaymentFailed;
        FailureReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status == OrderStatus.Shipped)
        {
            return Result.Failure(Error.Conflict("Order.CannotCancel", "Shipped order cannot be cancelled."));
        }

        Status = OrderStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }
}
