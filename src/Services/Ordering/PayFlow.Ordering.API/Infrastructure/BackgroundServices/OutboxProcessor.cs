using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PayFlow.Ordering.API.Infrastructure.Persistence;
using PayFlow.SharedKernel.Events;

namespace PayFlow.Ordering.API.Infrastructure.BackgroundServices;

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

            // Her 4 saniyede bir yeni mesaj kontrolü
            await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetService<IPublishEndpoint>();

        // Henüz RabbitMQ'ya iletilmemiş mesajları batch halinde (örn. 20 adet) al
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
                    // Mesaj tipine göre deserialize et ve RabbitMQ'ya publish et
                    if (message.Type.Contains(nameof(OrderCreatedIntegrationEvent)))
                    {
                        var @event = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(message.Content);
                        if (@event is not null)
                        {
                            await publishEndpoint.Publish(@event, cancellationToken);
                        }
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
