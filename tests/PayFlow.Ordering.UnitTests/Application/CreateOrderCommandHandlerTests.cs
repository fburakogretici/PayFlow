using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PayFlow.Ordering.API.Application.Orders.Commands.CreateOrder;
using PayFlow.Ordering.API.Infrastructure.Persistence;

namespace PayFlow.Ordering.UnitTests.Application;

public class CreateOrderCommandHandlerTests
{
    private readonly DbContextOptions<OrderDbContext> _dbOptions;
    private readonly Mock<ILogger<CreateOrderCommandHandler>> _loggerMock = new();

    public CreateOrderCommandHandlerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldSaveOrderAndOutboxMessageAtomically()
    {
        // Arrange
        await using var dbContext = new OrderDbContext(_dbOptions);
        var handler = new CreateOrderCommandHandler(dbContext, _loggerMock.Object);

        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "customer@domain.com",
            Street: "Bağdat Cad.",
            City: "İstanbul",
            Country: "Türkiye",
            ZipCode: "34728",
            Items: new List<CreateOrderItemDto>
            {
                new(Guid.NewGuid(), "Ergonomic Keyboard", 1500m, 1),
                new(Guid.NewGuid(), "Mousepad", 200m, 2)
            });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        // Veritabanında siparişin oluştuğunu doğrula
        var savedOrder = await dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == result.Value);
        savedOrder.Should().NotBeNull();
        savedOrder!.CustomerEmail.Should().Be("customer@domain.com");
        savedOrder.TotalAmount.Should().Be(1900m); // 1500 + 400

        // Transactional Outbox Pattern Kontrolü:
        // Outbox tablosunda bekleyen event mesajının oluşturulduğunu doğrula
        var outboxMessage = await dbContext.OutboxMessages.FirstOrDefaultAsync();
        outboxMessage.Should().NotBeNull();
        outboxMessage!.ProcessedOnUtc.Should().BeNull(); // Henüz işlenmemiş olmalı
        outboxMessage.Content.Should().Contain(savedOrder.Id.ToString());
    }

    [Fact]
    public async Task Handle_WithEmptyItems_ShouldFailAndNotSaveAnyData()
    {
        // Arrange
        await using var dbContext = new OrderDbContext(_dbOptions);
        var handler = new CreateOrderCommandHandler(dbContext, _loggerMock.Object);

        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "customer@domain.com",
            Street: "Bağdat Cad.",
            City: "İstanbul",
            Country: "Türkiye",
            ZipCode: "34728",
            Items: new List<CreateOrderItemDto>());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        var orderCount = await dbContext.Orders.CountAsync();
        var outboxCount = await dbContext.OutboxMessages.CountAsync();

        orderCount.Should().Be(0);
        outboxCount.Should().Be(0);
    }
}
