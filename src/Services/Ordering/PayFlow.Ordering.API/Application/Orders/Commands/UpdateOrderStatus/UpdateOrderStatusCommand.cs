using Microsoft.EntityFrameworkCore;
using PayFlow.Ordering.API.Infrastructure.Persistence;
using PayFlow.SharedKernel.CQRS;
using PayFlow.SharedKernel.Results;

namespace PayFlow.Ordering.API.Application.Orders.Commands.UpdateOrderStatus;

public record UpdateOrderPaymentStatusCommand(
    Guid OrderId,
    bool IsSuccess,
    string? TransactionId,
    string? FailureReason) : ICommand;

public class UpdateOrderPaymentStatusCommandHandler : ICommandHandler<UpdateOrderPaymentStatusCommand>
{
    private readonly OrderDbContext _dbContext;
    private readonly ILogger<UpdateOrderPaymentStatusCommandHandler> _logger;

    public UpdateOrderPaymentStatusCommandHandler(OrderDbContext dbContext, ILogger<UpdateOrderPaymentStatusCommandHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateOrderPaymentStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure(Error.NotFound("Order.NotFound", $"Order with id {request.OrderId} was not found."));
        }

        Result result;
        if (request.IsSuccess)
        {
            result = order.MarkAsPaid(request.TransactionId ?? "TXN_MOCK");
        }
        else
        {
            result = order.MarkAsPaymentFailed(request.FailureReason ?? "Payment declined.");
        }

        if (result.IsFailure)
        {
            return result;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} status updated to {Status}", order.Id, order.Status);
        return Result.Success();
    }
}
