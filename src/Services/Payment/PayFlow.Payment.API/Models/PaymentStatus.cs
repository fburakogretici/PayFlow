namespace PayFlow.Payment.API.Models;

/// <summary>
/// Ödeme durumu için tip-güvenli enum.
/// Magic String ("Approved", "Declined") kullanımını tamamen ortadan kaldırır.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Approved = 1,
    Declined = 2,
    Refunded = 3
}
