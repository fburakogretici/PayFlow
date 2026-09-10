using MassTransit;
using PayFlow.SharedKernel.Events;

namespace PayFlow.Notification.Worker.Consumers;

public class PaymentCompletedNotificationConsumer : IConsumer<PaymentCompletedIntegrationEvent>
{
    private readonly ILogger<PaymentCompletedNotificationConsumer> _logger;

    public PaymentCompletedNotificationConsumer(ILogger<PaymentCompletedNotificationConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PaymentCompletedIntegrationEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("""
            =============================================================
            [BİLDİRİM SERVİSİ] E-POSTA & SMS GÖNDERİLDİ!
            Sipariş Numarası: {OrderId}
            Ödeme Tutarı   : {Amount:N2} TRY
            Banka Onay Kodu: {TxnCode}
            Durum          : BAŞARILI
            Mesaj          : Sayın Müşterimiz, siparişiniz başarıyla alındı ve ödemeniz onaylandı.
            =============================================================
            """, msg.OrderId, msg.Amount, msg.TransactionId);

        return Task.CompletedTask;
    }
}

public class PaymentFailedNotificationConsumer : IConsumer<PaymentFailedIntegrationEvent>
{
    private readonly ILogger<PaymentFailedNotificationConsumer> _logger;

    public PaymentFailedNotificationConsumer(ILogger<PaymentFailedNotificationConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PaymentFailedIntegrationEvent> context)
    {
        var msg = context.Message;
        _logger.LogWarning("""
            =============================================================
            [BİLDİRİM SERVİSİ] ÖDEME BAŞARISIZ BİLDİRİMİ GÖNDERİLDİ!
            Sipariş Numarası: {OrderId}
            Tutar          : {Amount:N2} TRY
            Hata Nedeni    : {Reason}
            Durum          : BAŞARISIZ
            Mesaj          : Sayın Müşterimiz, siparişinizin ödemesi banka tarafından onaylanamadı.
            =============================================================
            """, msg.OrderId, msg.Amount, msg.Reason);

        return Task.CompletedTask;
    }
}
