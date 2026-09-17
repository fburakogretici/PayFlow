using MassTransit;
using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.API.Data;
using PayFlow.Payment.API.Models;
using PayFlow.Payment.API.Soap;
using PayFlow.SharedKernel.Events;

namespace PayFlow.Payment.API.Consumers;

public class OrderCreatedConsumer : IConsumer<OrderCreatedIntegrationEvent>
{
    private readonly PaymentDbContext _dbContext;
    private readonly IBankSoapAdapter _soapAdapter;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        PaymentDbContext dbContext,
        IBankSoapAdapter soapAdapter,
        IPublishEndpoint publishEndpoint,
        ILogger<OrderCreatedConsumer> logger)
    {
        _dbContext = dbContext;
        _soapAdapter = soapAdapter;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("OrderCreated eventi alındı. Sipariş ID: {OrderId}, Tutar: {Amount} TRY", message.OrderId, message.TotalAmount);

        // 1. Idempotency Kontrolü: Aynı mesaj tekrar gelirse mükerrer ödeme çekilmesini engelle
        var existingPayment = await _dbContext.PaymentRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == message.OrderId, context.CancellationToken);

        if (existingPayment is not null)
        {
            _logger.LogWarning(
                "Sipariş ID {OrderId} için ödeme zaten işlenmiş (Idempotency koruması). Durum: {Status}",
                message.OrderId, existingPayment.Status);
            return;
        }

        // 2. SOAP Banka Entegrasyonu üzerinden ödeme işlemi
        var soapResponse = await _soapAdapter.PayAsync(message.OrderId.ToString(), message.TotalAmount, context.CancellationToken);

        // 3. Rich Domain Model factory method — Magic String yok, domain mantığı encapsulate
        var paymentRecord = soapResponse.IsApproved
            ? PaymentRecord.CreateApproved(message.OrderId, message.TotalAmount, soapResponse.BankTransactionCode)
            : PaymentRecord.CreateDeclined(message.OrderId, message.TotalAmount, soapResponse.ResponseMessage);

        _dbContext.PaymentRecords.Add(paymentRecord);
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        // 4. Ödeme sonucunu RabbitMQ üzerinden diğer mikroservislere (Ordering, Notification) yayınla
        if (paymentRecord.IsApproved)
        {
            _logger.LogInformation("Ödeme SOAP bankası tarafından ONAYLANDI. TXN: {TxnCode}", soapResponse.BankTransactionCode);

            await _publishEndpoint.Publish(new PaymentCompletedIntegrationEvent(
                message.OrderId,
                soapResponse.BankTransactionCode,
                message.TotalAmount,
                message.CorrelationId), context.CancellationToken);
        }
        else
        {
            _logger.LogWarning("Ödeme SOAP bankası tarafından REDDEDİLDİ. Sebep: {Reason}", soapResponse.ResponseMessage);

            await _publishEndpoint.Publish(new PaymentFailedIntegrationEvent(
                message.OrderId,
                soapResponse.ResponseMessage,
                message.TotalAmount,
                message.CorrelationId), context.CancellationToken);
        }
    }
}
