using PayFlow.SharedKernel.Results;

namespace PayFlow.Ordering.API.Domain.Models;

/// <summary>
/// Order Aggregate'in öğesi. Factory method pattern ile tutarlı Result döndürür.
/// Exception throw etmek yerine Result&lt;T&gt; kullanarak tüm proje genelinde
/// hata yönetimi tutarlılığı sağlanır (Liskov Substitution uyumu).
/// </summary>
public class OrderItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal TotalPrice => UnitPrice * Quantity;

    private OrderItem() { } // EF Core için

    /// <summary>
    /// Factory Method: Domain kurallarını uygular ve Result döndürür.
    /// Tüm proje genelinde exception yerine Result pattern kullanılır.
    /// </summary>
    public static Result<OrderItem> Create(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return Result.Failure<OrderItem>(Error.Validation("OrderItem.NameRequired", "Product name is required."));

        if (quantity <= 0)
            return Result.Failure<OrderItem>(Error.Validation("OrderItem.InvalidQuantity", $"Quantity must be greater than zero. Got: {quantity}"));

        if (unitPrice < 0)
            return Result.Failure<OrderItem>(Error.Validation("OrderItem.InvalidPrice", "Unit price cannot be negative."));

        return Result.Success(new OrderItem
        {
            ProductId = productId,
            ProductName = productName.Trim(),
            UnitPrice = unitPrice,
            Quantity = quantity
        });
    }

    // Backward-compatibility: Aggregate içinden doğrudan çağırmak için internal constructor
    // Bunu sadece Order.Create kullanır; dışarıdan OrderItem.Create() kullanılmalıdır.
    internal OrderItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}
