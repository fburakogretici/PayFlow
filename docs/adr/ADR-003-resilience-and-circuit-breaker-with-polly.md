# ADR-003: Dağıtık Hata Yönetiminde Polly ve Circuit Breaker Stratejisi

## Durum (Status)
**Kabul Edildi (Accepted)**

## Bağlam ve Problem (Context & Problem)
PayFlow ödeme mikroservisi (`Payment.API`), harici bir banka SOAP servisine bağlanarak kredi kartı çekim işlemlerini onaylatmaktadır. Dağıtık ortamlarda dış servisler geçici ağ gecikmeleri, timeout veya tamamen servis kesintileri yaşayabilir.

Harici bir servis çöktüğünde geleneksel istek modellerinde:
- İstek atan thread'ler kilitlenir ve thread pool tükenir (Cascading Failure / Çığ Etkisi),
- Tüm sistem donar ve ödeme yapamayan kullanıcılar nedeniyle sipariş servisi de tıkanır.

## Alınan Karar (Decision)
**Polly v8 Resilience Pipeline** kullanılarak çok katmanlı dayanıklılık stratejisi uygulanmıştır:
1. **Timeout:** Dış banka servisine yapılan çağrılar 5 saniye ile sınırlandırılmıştır.
2. **Exponential Backoff with Jitter:** Anlık ağ dalgalanmalarında 3 kez üstel gecikmeli tekrar deneme (Retry) yapılır. Aynı anda binlerce isteğin bankaya yığılmasını engellemek için rassal sapma (Jitter) eklenmiştir.
3. **Circuit Breaker (Devre Kesici):** Başarısızlık oranı %50'yi aştığında devre açılır (`Circuit OPEN`). 15 saniye boyunca bankaya istek gönderilmeyerek hem banka üzerindeki yük azaltılır hem de uygulama kaynakları kilitlenmekten korunur.
4. **Graceful Fallback:** Önbellek tarafında (`CatalogCacheService`), Redis erişilemez hale geldiğinde 500 hatası vermek yerine sessizce veritabanına geri dönülür (Graceful Degradation).

## Sonuçlar ve Değerlendirme (Consequences)
- **Pozitif:** Dış banka arızası PayFlow servislerinin kaynaklarını tüketmez ve çökertmez.
- **Pozitif:** Anlık mikro kesintiler kullanıcıya hissettirilmeden şeffaf şekilde tolere edilir.
