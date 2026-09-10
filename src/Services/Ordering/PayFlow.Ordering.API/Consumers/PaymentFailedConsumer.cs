using MassTransit;
using MediatR;
using PayFlow.Ordering.API.Application.Orders.Commands.UpdateOrderStatus;
using PayFlow.SharedKernel.Events;

namespace PayFlow.Ordering.API.Consumers;

public class PaymentFailedConsumer : IConsumer<PaymentFailedIntegrationEvent>
{
    private readonly ISender _sender;
    private readonly ILogger<PaymentFailedConsumer> _logger;

    public PaymentFailedConsumer(ISender sender, ILogger<PaymentFailedConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentFailedIntegrationEvent> context)
    {
        var message = context.Message;
        _logger.LogWarning("Ödeme başarısız eventi alındı: Sipariş ID: {OrderId}, Neden: {Reason}", message.OrderId, message.Reason);

        var command = new UpdateOrderPaymentStatusCommand(
            message.OrderId,
            IsSuccess: false,
            TransactionId: null,
            FailureReason: message.Reason);

        var result = await _sender.Send(command, context.CancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError("Sipariş ödeme hata durumu güncellenemedi: {Error}", result.Error.Message);
        }
    }
}
