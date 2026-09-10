using MassTransit;
using MediatR;
using PayFlow.Ordering.API.Application.Orders.Commands.UpdateOrderStatus;
using PayFlow.SharedKernel.Events;

namespace PayFlow.Ordering.API.Consumers;

public class PaymentCompletedConsumer : IConsumer<PaymentCompletedIntegrationEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<PaymentCompletedConsumer> _logger;

    public PaymentCompletedConsumer(ISender sender, ILogger<PaymentCompletedConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCompletedIntegrationEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Ödeme tamamlandı eventi alındı: Sipariş ID: {OrderId}, TXN: {Txn}", message.OrderId, message.TransactionId);

        var command = new UpdateOrderPaymentStatusCommand(
            message.OrderId,
            IsSuccess: true,
            TransactionId: message.TransactionId,
            FailureReason: null);

        var result = await _sender.Send(command, context.CancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError("Sipariş ödeme durumu güncellenemedi: {Error}", result.Error.Message);
        }
    }
}
