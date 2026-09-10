namespace PayFlow.Ordering.API.Domain.Enums;

public enum OrderStatus
{
    PendingPayment = 1,
    Paid = 2,
    PaymentFailed = 3,
    Shipped = 4,
    Cancelled = 5
}
