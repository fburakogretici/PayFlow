using MassTransit;
using PayFlow.SharedKernel.Events;

namespace PayFlow.Ordering.API.Infrastructure.BackgroundServices;

/// <summary>
/// Outbox mesajı yayınlama stratejisini soyutlar.
/// Yeni event tipleri için OutboxProcessor dokunulmadan yeni implementasyon eklenebilir.
/// (Open/Closed Principle)
/// </summary>
public interface IOutboxMessagePublisher
{
    string MessageTypeName { get; }
    Task PublishAsync(string content, IPublishEndpoint publishEndpoint, CancellationToken cancellationToken);
}
