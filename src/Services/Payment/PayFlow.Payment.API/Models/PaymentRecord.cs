namespace PayFlow.Payment.API.Models;

/// <summary>
/// Rich Domain Model: Anemic anti-pattern yerine,
/// domain mantığını private setter'lar ve factory method aracılığıyla encapsulate eder.
/// Dışarıdan doğrudan property ataması yapılamaz; sadece tanımlı davranışlar üzerinden mutasyon gerçekleşir.
/// </summary>
public class PaymentRecord
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string BankTransactionCode { get; private set; } = string.Empty;
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PaymentRecord() { } // EF Core için

    /// <summary>
    /// Factory Method: Tüm domain kurallarıyla birlikte başarılı bir ödeme kaydı oluşturur.
    /// </summary>
    public static PaymentRecord CreateApproved(Guid orderId, decimal amount, string bankTransactionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bankTransactionCode);

        return new PaymentRecord
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Status = PaymentStatus.Approved,
            BankTransactionCode = bankTransactionCode,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory Method: Reddedilen bir ödeme kaydı oluşturur.
    /// </summary>
    public static PaymentRecord CreateDeclined(Guid orderId, decimal amount, string reason)
    {
        return new PaymentRecord
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Status = PaymentStatus.Declined,
            FailureReason = reason,
            BankTransactionCode = string.Empty,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>Domain kuralı: Onaylı ödeme iade edilebilir.</summary>
    public void Refund()
    {
        if (Status != PaymentStatus.Approved)
            throw new InvalidOperationException($"Only approved payments can be refunded. Current status: {Status}");

        Status = PaymentStatus.Refunded;
    }

    public bool IsApproved => Status == PaymentStatus.Approved;
    public bool IsDeclined => Status == PaymentStatus.Declined;

    // EF Core JSON projeksiyonu için status string
    public string StatusText => Status.ToString();
}
