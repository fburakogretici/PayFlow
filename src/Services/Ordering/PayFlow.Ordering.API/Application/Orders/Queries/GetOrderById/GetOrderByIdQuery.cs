using Microsoft.EntityFrameworkCore;
using PayFlow.Ordering.API.Domain.Enums;
using PayFlow.Ordering.API.Infrastructure.Persistence;
using PayFlow.SharedKernel.CQRS;
using PayFlow.SharedKernel.Results;

namespace PayFlow.Ordering.API.Application.Orders.Queries.GetOrderById;

public record OrderItemResponse(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal TotalPrice);

public record OrderDetailResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerEmail,
    string Street,
    string City,
    string Country,
    string ZipCode,
    string Status,
    decimal TotalAmount,
    string? PaymentTransactionId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderItemResponse> Items);

public record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderDetailResponse>;

public class GetOrderByIdQueryHandler : IQueryHandler<GetOrderByIdQuery, OrderDetailResponse>
{
    private readonly OrderDbContext _dbContext;

    public GetOrderByIdQueryHandler(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<OrderDetailResponse>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderDetailResponse>(
                Error.NotFound("Order.NotFound", $"Order with ID {request.OrderId} was not found."));
        }

        var response = new OrderDetailResponse(
            order.Id,
            order.CustomerId,
            order.CustomerEmail,
            order.ShippingAddress.Street,
            order.ShippingAddress.City,
            order.ShippingAddress.Country,
            order.ShippingAddress.ZipCode,
            order.Status.ToString(),
            order.TotalAmount,
            order.PaymentTransactionId,
            order.FailureReason,
            order.CreatedAtUtc,
            order.Items.Select(i => new OrderItemResponse(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.TotalPrice)).ToList());

        return Result.Success(response);
    }
}
