using System.Text.Json;
using MassTransit;
using PayFlow.SharedKernel.Events;

namespace PayFlow.Ordering.API.Infrastructure.BackgroundServices;

/// <summary>
/// OrderCreatedIntegrationEvent için Outbox yayınlama stratejisi.
/// Yeni event tipi için sadece bu sınıfa benzer yeni bir implementasyon yaratılır;
/// OutboxProcessor'a dokunulmaz. (Open/Closed Principle)
/// </summary>
public sealed class OrderCreatedOutboxPublisher : IOutboxMessagePublisher
{
    public string MessageTypeName => nameof(OrderCreatedIntegrationEvent);

    public async Task PublishAsync(string content, IPublishEndpoint publishEndpoint, CancellationToken cancellationToken)
    {
        var @event = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(content);
        if (@event is not null)
        {
            await publishEndpoint.Publish(@event, cancellationToken);
        }
    }
}
