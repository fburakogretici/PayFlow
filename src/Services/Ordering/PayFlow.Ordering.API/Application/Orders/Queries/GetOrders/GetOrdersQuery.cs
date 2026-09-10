using Microsoft.EntityFrameworkCore;
using PayFlow.Ordering.API.Domain.Enums;
using PayFlow.Ordering.API.Infrastructure.Persistence;
using PayFlow.SharedKernel.CQRS;
using PayFlow.SharedKernel.Results;

namespace PayFlow.Ordering.API.Application.Orders.Queries.GetOrders;

public record OrderSummaryResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerEmail,
    string Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAtUtc);

public record GetOrdersQuery(Guid? CustomerId = null) : IQuery<IReadOnlyList<OrderSummaryResponse>>;

public class GetOrdersQueryHandler : IQueryHandler<GetOrdersQuery, IReadOnlyList<OrderSummaryResponse>>
{
    private readonly OrderDbContext _dbContext;

    public GetOrdersQueryHandler(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<OrderSummaryResponse>>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Orders.AsNoTracking();

        if (request.CustomerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == request.CustomerId.Value);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new OrderSummaryResponse(
                o.Id,
                o.CustomerId,
                o.CustomerEmail,
                o.Status.ToString(),
                o.TotalAmount,
                o.Items.Count,
                o.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<OrderSummaryResponse>>(orders);
    }
}
