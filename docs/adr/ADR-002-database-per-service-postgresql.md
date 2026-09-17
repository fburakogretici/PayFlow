# ADR-002: Database-per-Service ve PostgreSQL Mimarisi

## Durum (Status)
**Kabul Edildi (Accepted)**

## Bağlam ve Problem (Context & Problem)
Monolitik sistemlerde tek bir merkezi veritabanı kullanılır ve tüm tablolar aynı şema altında birbiriyle foreign key ilişkileri kurar. Ancak mikroservis mimarisinde ortak bir veritabanı kullanmak (**Shared Database Anti-Pattern**):
- Servisler arasında sıkı bağa (tight coupling) neden olur,
- Bir servisin yapacağı şema değişikliği diğer servisleri kırar,
- Bağımsız ölçeklenebilirliği (horizontal scaling) ve bağımsız deployment'ı engeller.

## Alınan Karar (Decision)
Mikroservis bağımsızlığını garanti altına almak için **Database-per-Service** prensibi uygulanmıştır:
1. `Catalog.API`, `Ordering.API` ve `Payment.API` servisleri tamamen kendilerine ait izole veritabanlarına (`payflow_catalog_db`, `payflow_ordering_db`, `payflow_payment_db`) sahiptir.
2. Servisler birbirlerinin veritabanlarına **kesinlikle doğrudan SQL sorgusu atamaz**. Veri ihtiyacı REST API veya RabbitMQ Event Bus üzerinden asenkron mesajlaşma ile çözülür.
3. Üretim standardı olarak yüksek performanslı, ACID uyumlu **PostgreSQL 16** seçilmiştir. Lokal test senaryolarında konteyner çalışmadığı durumlarda ise SQLite fallback desteği sağlanmıştır.

## Sonuçlar ve Değerlendirme (Consequences)
- **Pozitif:** Her servis kendi veri modelini diğerlerinden bağımsız güncelleyebilir ve versiyonlayabilir.
- **Pozitif:** Sipariş trafiği patladığında yalnızca `OrderingDb` ölçeklendirilebilir; katalog okumaları bundan etkilenmez.
- **Negatif / Yönetilmesi Gereken:** Tablolar arası SQL `JOIN` yapılamaz. Bunun yerine Event-Driven mimari ile veriler olaylar üzerinden güncellenir ve senkronize edilir.
