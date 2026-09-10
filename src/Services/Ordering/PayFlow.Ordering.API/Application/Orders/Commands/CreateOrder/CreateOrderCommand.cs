using System.Text.Json;
using PayFlow.Ordering.API.Domain.Models;
using PayFlow.Ordering.API.Infrastructure.Persistence;
using PayFlow.SharedKernel.CQRS;
using PayFlow.SharedKernel.Events;
using PayFlow.SharedKernel.Outbox;
using PayFlow.SharedKernel.Results;

namespace PayFlow.Ordering.API.Application.Orders.Commands.CreateOrder;

public record CreateOrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);

public record CreateOrderCommand(
    Guid CustomerId,
    string CustomerEmail,
    string Street,
    string City,
    string Country,
    string ZipCode,
    List<CreateOrderItemDto> Items) : ICommand<Guid>;

public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    private readonly OrderDbContext _dbContext;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(OrderDbContext dbContext, ILogger<CreateOrderCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // 1. DDD Value Object ve Entity'lerin oluşturulması
        var address = new Address(request.Street, request.City, request.Country, request.ZipCode);
        var orderItems = request.Items.Select(i => new OrderItem(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity));

        var orderResult = Order.Create(request.CustomerId, request.CustomerEmail, address, orderItems);
        if (orderResult.IsFailure)
        {
            return Result.Failure<Guid>(orderResult.Error);
        }

        var order = orderResult.Value;

        // 2. Transactional Outbox Pattern:
        // Siparişi ve Outbox mesajını AYNI veri tabanı transaction'ında kaydediyoruz.
        // Bu sayede RabbitMQ geçici olarak kapalı olsa bile sipariş kaybolmaz ve çift yazma (dual-write) hatası oluşmaz!
        var integrationEvent = new OrderCreatedIntegrationEvent(
            order.Id,
            order.CustomerEmail,
            order.TotalAmount,
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity)).ToList());

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = typeof(OrderCreatedIntegrationEvent).AssemblyQualifiedName ?? typeof(OrderCreatedIntegrationEvent).Name,
            Content = JsonSerializer.Serialize(integrationEvent),
            OccurredOnUtc = DateTime.UtcNow
        };

        _dbContext.Orders.Add(order);
        _dbContext.OutboxMessages.Add(outboxMessage);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} successfully created and Outbox message queued.", order.Id);

        return Result.Success(order.Id);
    }
}
