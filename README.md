# PayFlow: Senior .NET 9 Mikroservis Mimarisi Platformu

Bu proje, yüksek trafikli (10.000+ kullanıcı) sistemlerde kullanılan modern dağıtık yazılım mimarilerini, tasarım kalıplarını ve kıdemli (Senior) .NET mühendisliği pratiklerini birebir deneyimlemek amacıyla hazırlanmıştır.

---

## 🏛️ Mimari Tasarım & Akış

```mermaid
graph TD
    Client([İstemci / Web / Mobil]) --> Gateway[API Gateway - YARP\nRate Limiting + Routing]
    
    Gateway -->|GET /api/catalog| CatalogAPI[Catalog.API\nEF Core + Redis Cache-Aside]
    Gateway -->|POST /api/orders| OrderAPI[Ordering.API\nClean Arch + CQRS + Outbox]
    
    OrderAPI -.->|1. Outbox Event:\nOrderCreatedIntegrationEvent| RabbitMQ[(RabbitMQ Event Bus)]
    
    RabbitMQ -.->|2. Consume Event| PaymentAPI[Payment.API\nMassTransit Consumer]
    
    PaymentAPI -->|3. SOAP XML Request\n(WCF / BasicHttpBinding)| LegacyBank[Legacy Bank SOAP Service]
    
    PaymentAPI -.->|4. Publish Event:\nPaymentCompleted / Failed| RabbitMQ
    
    RabbitMQ -.->|5. Consume Event| OrderAPI
    RabbitMQ -.->|6. Consume Event| NotifWorker[Notification.Worker\nBackground Worker]
```

---

## 🚀 Öne Çıkan Teknolojiler & Kütüphaneler

- **.NET 9 / C# 13**: En güncel dil özellikleri (Primary Constructors, Collection Expressions, Minimal APIs).
- **YARP (Yet Another Reverse Proxy)**: Microsoft'un yüksek performanslı modern API Gateway çözümü ve Rate Limiter.
- **MassTransit & RabbitMQ**: Mikroservisler arası asenkron, gevşek bağlı (loosely coupled) event-driven iletişim.
- **Redis (StackExchange.Redis)**: 10.000+ kullanıcı okuma trafiği için **Cache-Aside Pattern** önbellekleme.
- **Entity Framework Core 9**: `AsNoTracking()` optimizasyonları, Owned Entity (Value Objects) ve veritabanı indeksleri.
- **WCF / SOAP Client (`System.ServiceModel`)**: Banka/kurumsal sistemlerle XML/SOAP tabanlı legacy entegrasyon.
- **MediatR (CQRS)**: Komut (Command) ve sorgu (Query) sorumluluklarının ayrılması.
- **Transactional Outbox Pattern**: Dağıtık sistemlerde veri kaybını ve çift yazma (dual-write) problemini çözen arka plan kuyruğu.
- **xUnit, FluentAssertions & Moq**: Kapsamlı birim ve domain testleri.
- **Docker Compose & Kubernetes (k8s)**: Çok aşamalı (multi-stage) Dockerfile'lar ve K8s dağıtım manifestleri.
- **CI/CD**: GitHub Actions otomatik derleme ve test iş akışı.

---

## 💡 Mülakatlarda Seni Öne Çıkaracak Kritik Mimari Kalıplar

### 1. Transactional Outbox Pattern Nedir ve Neden Kullandık?
> **Problem:** Bir sipariş oluşturulduğunda siparişi veritabanına kaydedip hemen ardından RabbitMQ'ya mesaj fırlatırsanız (**Dual-Write**), RabbitMQ o an çökerse sipariş kaydedilir ama ödeme ve bildirim asla tetiklenmez. Dağıtık sistemlerde veri tutarsızlığı doğar.
> 
> **Çözümümüz:** `CreateOrderCommandHandler` içinde hem `Order` hem de `OutboxMessage` nesnesi **aynı yerel veritabanı transaction'ında** kaydedilir. Arka planda çalışan `OutboxProcessor` (BackgroundService) işlenmemiş mesajları alır, RabbitMQ'ya gönderir ve `ProcessedOnUtc` alanını günceller. Mesaj iletimi %100 garanti altına alınır (At-Least-Once Delivery).

### 2. Cache-Aside Pattern & Graceful Degradation (10.000+ Kullanıcı Ölçeği)
> `Catalog.API` servisinde ürün listesi her istekte veritabanına gitmez:
> 1. Önce Redis kontrol edilir (Sub-millisecond).
> 2. Cache Miss durumunda DB'den `AsNoTracking()` ile hızlıca çekilip Redis'e 10 dk TTL ile yazılır.
> 3. Ürün güncellendiğinde veya eklendiğinde `InvalidateCacheAsync` ile önbellek temizlenir.
> 4. **Resilience:** Redis kapalı olsa bile sistem 500 hatası vermez; log yazıp veritabanına sorunsuz fallback yapar (**Graceful Degradation**).

### 3. Dağıtık Sistemlerde Idempotency (Mükerrer İstek Koruması)
> RabbitMQ gibi mesajlaşma sistemleri "At-Least-Once" garanti verir, yani bir mesaj iki kez iletilebilir.
> `Payment.API` servisinde `OrderId` alanı veritabanında **Unique Index** ile korunur. Tüketici (`OrderCreatedConsumer`) aynı sipariş için ikinci kez event aldığında ödeme işlemini tekrarlamaz, böylece müşteriden mükerrer para çekilmesi engellenir.

### 4. SOAP ve REST Servis Birlikteliği
> Modern dünya RESTful JSON API'lar kullanırken bankalar, kamu kurumları ve ERP sistemleri genellikle WCF/SOAP XML servisleri kullanır.
> `PayFlow.Payment.API`, `System.ServiceModel.Http` kullanarak `BasicHttpBinding` ile SOAP zarfı oluşturur ve bankayla haberleşir.

---

## 🛠️ Nasıl Çalıştırılır?

### Seçenek 1: Docker Compose ile Tek Komutla Ayağa Kaldırma (Tavsiye Edilen)
Tüm altyapıyı (RabbitMQ, Redis, Mikroservisler ve Gateway) tek bir komutla ayağa kaldırabilirsiniz:

```bash
docker compose up -d --build
```

Servislerin erişim adresleri:
- **API Gateway (Ana Giriş):** `http://localhost:5000`
- **Catalog API (Swagger):** `http://localhost:5001`
- **Ordering API (Swagger):** `http://localhost:5002`
- **Payment API (Swagger):** `http://localhost:5003`
- **RabbitMQ Yönetim Paneli:** `http://localhost:15672` (Kullanıcı/Şifre: `guest` / `guest`)

---

### Seçenek 2: Lokal Geliştirme Ortamında Çalıştırma

1. **Testleri Çalıştırma:**
   ```bash
   dotnet test
   ```

2. **Catalog Servisini Başlatma:**
   ```bash
   dotnet run --project src/Services/Catalog/PayFlow.Catalog.API
   ```

3. **Ordering Servisini Başlatma:**
   ```bash
   dotnet run --project src/Services/Ordering/PayFlow.Ordering.API
   ```

4. **Payment Servisini Başlatma:**
   ```bash
   dotnet run --project src/Services/Payment/PayFlow.Payment.API
   ```

5. **Notification Servisini Başlatma:**
   ```bash
   dotnet run --project src/Services/Notification/PayFlow.Notification.Worker
   ```

6. **API Gateway'i Başlatma:**
   ```bash
   dotnet run --project src/ApiGateways/PayFlow.ApiGateway
   ```
