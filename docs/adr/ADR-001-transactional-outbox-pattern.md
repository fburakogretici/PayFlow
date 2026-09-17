# ADR-001: Transactional Outbox Pattern Kullanımı

## Durum (Status)
**Kabul Edildi (Accepted)**

## Bağlam ve Problem (Context & Problem)
PayFlow platformunda bir sipariş oluşturulduğunda (`CreateOrderCommand`), sipariş bilgilerinin veritabanına kalıcı olarak yazılması ve diğer mikroservislerin (Payment, Notification) haberdar edilmesi için RabbitMQ mesaj kuyruğuna bir `OrderCreatedIntegrationEvent` fırlatılması gerekmektedir.

Geleneksel yaklaşımda, önce veritabanına `SaveChanges` yapılır, hemen ardından `IPublishEndpoint.Publish` çağrılır. Bu duruma **Dual-Write (Çift Yazma)** problemi denir:
- Veritabanına yazılıp hemen ardından RabbitMQ bağlantısı koparsa veya broker çökerse, sipariş kaydedilir ancak ödeme ve bildirim asla tetiklenmez (Veri tutarsızlığı).
- Dağıtık transaction'lar (2PC - Two Phase Commit) ise bulut ve mikroservis mimarilerinde aşırı yavaş, kilitlenmelere açık ve anti-pattern kabul edilir.

## Alınan Karar (Decision)
Veri bütünlüğünü (Data Consistency) sağlamak için **Transactional Outbox Pattern** benimsenmiştir:
1. Sipariş entity'si ve dışarıya fırlatılacak event (`OutboxMessage`), **aynı yerel veritabanı transaction'ı içerisinde** atomik olarak kaydedilir.
2. Arka planda çalışan bir `IHostedService` (`OutboxProcessor`), işlenmemiş outbox kayıtlarını periyodik olarak okur, RabbitMQ'ya güvenle publish eder ve kaydı `ProcessedOnUtc` ile işaretler.
3. Bu sayede **At-Least-Once Delivery** (En az bir kez iletim) garantisi elde edilir.

## Sonuçlar ve Değerlendirme (Consequences)
- **Pozitif:** RabbitMQ çökse dahi hiçbir sipariş ve ödeme eventi kaybolmaz. Broker ayağa kalktığında OutboxProcessor kaldığı yerden iletime devam eder.
- **Pozitif:** Dağıtık kilit (distributed lock) veya ağır 2PC protokollerine gerek kalmaz.
- **Negatif / Yönetilmesi Gereken:** "At-Least-Once" iletimi nedeniyle tüketici (Consumer) tarafında mükerrer mesaj işlenmesini önlemek için **Idempotent Consumer** mekanizması kurulmalıdır (`Payment.API` servisinde `OrderId` unique index kontrolü ile çözülmüştür).
