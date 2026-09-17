# ADR-004: Gözlemlenebilirlik (Observability) ve Seq ile Dağıtık İzleme

## Durum (Status)
**Kabul Edildi (Accepted)**

## Bağlam ve Problem (Context & Problem)
Mikroservis mimarisinde tek bir kullanıcı işlemi (örneğin sipariş verme) birden fazla bağımsız servisten (API Gateway -> Ordering -> RabbitMQ -> Payment -> SOAP Bank -> Notification) geçer.

Hata durumunda geleneksel dosya bazlı loglama (`app.log`):
- Hatayı hangi servisin ve hangi adımın tetiklediğini bulmayı imkansız kılar,
- Asenkron event kuyruğuna giren bir işlemin akışını takip etmeyi zorlaştırır.

## Alınan Karar (Decision)
**Serilog & Seq** entegrasyonu ile merkezi ve yapılandırılmış loglama (Structured Logging) ile dağıtık izleme kurulmuştur:
1. **Correlation ID:** İstemciden gelen `X-Correlation-ID` header'ı Gateway'deki `CorrelationIdMiddleware` tarafından yakalanır veya yoksa yeni bir GUID üretilir.
2. Bu Correlation ID hem HTTP çağrılarına hem de RabbitMQ mesaj başlıklarına aktarılır.
3. Serilog'un `LogContext` mekanizması ile her bir log satırı `{CorrelationId, Application, Timestamp}` meta verisiyle zenginleştirilir.
4. Tüm mikroservisler loglarını tek bir merkezi **Seq** (`http://localhost:5341`) arayüzüne basar.

## Sonuçlar ve Değerlendirme (Consequences)
- **Pozitif:** Mülakatta veya canlı operasyonda Seq paneline bir `CorrelationId` yazılarak bir siparişin Gateway'den bildirime kadar tüm yaşam döngüsü saniyeler içinde görselleştirilebilir.
- **Pozitif:** Dağıtık sistemlerde hata kök neden analizi (Root Cause Analysis - RCA) süresi saatlerden saniyelere iner.
