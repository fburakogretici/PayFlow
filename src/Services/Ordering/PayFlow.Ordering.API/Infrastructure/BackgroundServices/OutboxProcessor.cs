using MassTransit;
using Microsoft.EntityFrameworkCore;
using PayFlow.Ordering.API.Infrastructure.Persistence;

namespace PayFlow.Ordering.API.Infrastructure.BackgroundServices;

/// <summary>
/// Transactional Outbox Pattern: Veri tabanında bekleyen mesajları RabbitMQ'ya iletir.
///
/// OCP Uyumu: if/else if zinciri yerine IOutboxMessagePublisher Strategy listesi kullanır.
/// Yeni event tipi için sadece yeni bir IOutboxMessagePublisher implementasyonu DI'ya eklenir;
/// bu sınıfa dokunulmaz.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Transactional Outbox Processor background worker başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox mesajları işlenirken hata oluştu.");
            }

            await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetService<IPublishEndpoint>();

        // DI'dan tüm kayıtlı publisher stratejilerini al
        // Yeni event tipi → sadece DI'ya yeni IOutboxMessagePublisher implementasyonu ekle
        var publishers = scope.ServiceProvider
            .GetServices<IOutboxMessagePublisher>()
            .ToDictionary(p => p.MessageTypeName, StringComparer.OrdinalIgnoreCase);

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0) return;

        _logger.LogInformation("{Count} adet işlenmemiş Outbox mesajı bulundu, RabbitMQ'ya iletiliyor...", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                if (publishEndpoint is not null)
                {
                    // Strategy Dispatcher: Mesaj tipine göre doğru publisher'ı bul ve çalıştır
                    var publisher = publishers.FirstOrDefault(kvp => message.Type.Contains(kvp.Key)).Value;

                    if (publisher is not null)
                    {
                        await publisher.PublishAsync(message.Content, publishEndpoint, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Outbox mesajı ({MessageId}) için kayıtlı publisher bulunamadı. Tip: {Type}", message.Id, message.Type);
                    }
                }

                message.MarkAsProcessed();
                _logger.LogInformation("Outbox mesajı ({MessageId}) başarıyla işlendi.", message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbox mesajı ({MessageId}) RabbitMQ'ya gönderilemedi. Bir sonraki turda tekrar denenecek.", message.Id);
                message.MarkAsFailed(ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
