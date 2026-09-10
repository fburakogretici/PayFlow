using FluentAssertions;
using PayFlow.Ordering.API.Domain.Enums;
using PayFlow.Ordering.API.Domain.Models;

namespace PayFlow.Ordering.UnitTests.Domain;

public class OrderAggregateTests
{
    private readonly Address _validAddress = new("İstiklal Cad. No:10", "İstanbul", "Türkiye", "34000");

    [Fact]
    public void Create_WithValidData_ShouldReturnSuccessWithPendingPaymentStatus()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var email = "burak@example.com";
        var items = new List<OrderItem>
        {
            new(Guid.NewGuid(), "Gaming Laptop", 25000m, 1),
            new(Guid.NewGuid(), "Mouse", 500m, 2)
        };

        // Act
        var result = Order.Create(customerId, email, _validAddress, items);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.CustomerEmail.Should().Be(email);
        result.Value.Status.Should().Be(OrderStatus.PendingPayment);
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalAmount.Should().Be(26000m); // 25000 + (500 * 2)
    }

    [Fact]
    public void Create_WithoutItems_ShouldReturnFailureResult()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var email = "burak@example.com";
        var emptyItems = new List<OrderItem>();

        // Act
        var result = Order.Create(customerId, email, _validAddress, emptyItems);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.EmptyItems");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidEmail_ShouldReturnFailureResult(string? invalidEmail)
    {
        // Arrange
        var items = new List<OrderItem> { new(Guid.NewGuid(), "Book", 100m, 1) };

        // Act
        var result = Order.Create(Guid.NewGuid(), invalidEmail!, _validAddress, items);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.EmailRequired");
    }

    [Fact]
    public void MarkAsPaid_WhenOrderIsPendingPayment_ShouldSucceedAndSetTransaction()
    {
        // Arrange
        var items = new List<OrderItem> { new(Guid.NewGuid(), "Book", 100m, 1) };
        var order = Order.Create(Guid.NewGuid(), "test@domain.com", _validAddress, items).Value;

        // Act
        var result = order.MarkAsPaid("TXN_SOAP_12345");

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid);
        order.PaymentTransactionId.Should().Be("TXN_SOAP_12345");
        order.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsPaid_WhenOrderIsAlreadyPaid_ShouldReturnConflictFailure()
    {
        // Arrange
        var items = new List<OrderItem> { new(Guid.NewGuid(), "Book", 100m, 1) };
        var order = Order.Create(Guid.NewGuid(), "test@domain.com", _validAddress, items).Value;
        order.MarkAsPaid("TXN_1");

        // Act: Aynı sipariş tekrar onaylanmaya çalışılırsa
        var result = order.MarkAsPaid("TXN_2");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.InvalidState");
    }

    [Fact]
    public void Cancel_WhenOrderIsNotShipped_ShouldSucceed()
    {
        // Arrange
        var items = new List<OrderItem> { new(Guid.NewGuid(), "Book", 100m, 1) };
        var order = Order.Create(Guid.NewGuid(), "test@domain.com", _validAddress, items).Value;

        // Act
        var result = order.Cancel();

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }
}
