using System.ServiceModel;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace PayFlow.Payment.API.Soap;

public interface IBankSoapAdapter
{
    Task<BankSoapPaymentResponse> PayAsync(string orderReference, decimal amount, CancellationToken cancellationToken = default);
}

public class BankSoapAdapter : IBankSoapAdapter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BankSoapAdapter> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public BankSoapAdapter(IConfiguration configuration, ILogger<BankSoapAdapter> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Polly v8 Enterprise Resilience Pipeline:
        // 1. Timeout (5 saniye)
        // 2. Exponential Retry with Jitter (3 deneme)
        // 3. Circuit Breaker (Hata oranı %50'yi aşarsa devreyi 15 sn açar, sistemi korur)
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(5)
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(400),
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning("Banka SOAP çağrısı başarısız oldu, yeniden deneniyor ({Attempt}. deneme): {Message}",
                        args.AttemptNumber, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(15),
                OnOpened = args =>
                {
                    logger.LogError("Banka SOAP Circuit Breaker AÇILDI (Circuit OPEN). 15 saniye boyunca bankaya yeni istek atılmayacak.");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("Banka SOAP Circuit Breaker KAPANDI (Circuit CLOSED). Servis normale döndü.");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<BankSoapPaymentResponse> PayAsync(string orderReference, decimal amount, CancellationToken cancellationToken = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            return await ExecutePaymentSoapCallAsync(orderReference, amount, ct);
        }, cancellationToken);
    }

    private async Task<BankSoapPaymentResponse> ExecutePaymentSoapCallAsync(string orderReference, decimal amount, CancellationToken cancellationToken)
    {
        var soapEndpointUrl = _configuration["BankSoap:EndpointUrl"];

        // 1. Canlı SOAP Servisi URL'i tanımlıysa gerçek WCF ChannelFactory üzerinden SOAP/XML isteği gönder:
        if (!string.IsNullOrEmpty(soapEndpointUrl) && Uri.TryCreate(soapEndpointUrl, UriKind.Absolute, out _))
        {
            var binding = new BasicHttpBinding
            {
                SendTimeout = TimeSpan.FromSeconds(30),
                ReceiveTimeout = TimeSpan.FromSeconds(30)
            };

            var endpoint = new EndpointAddress(soapEndpointUrl);
            var factory = new ChannelFactory<IBankSoapService>(binding, endpoint);
            var client = factory.CreateChannel();

            var request = new BankSoapPaymentRequest
            {
                MerchantId = _configuration["BankSoap:MerchantId"] ?? "MERCHANT_1001",
                OrderReference = orderReference,
                Amount = amount,
                Currency = "TRY"
            };

            return await client.ProcessPaymentAsync(request);
        }

        // 2. Lokal/Test Geliştirme Ortamı: Gerçekçi SOAP yanıt simülasyonu
        _logger.LogInformation("[SOAP Entegrasyonu] Banka SOAP servisine XML isteği iletiliyor (WCF BasicHttpBinding). Sipariş: {Ref}, Tutar: {Amount} TRY", orderReference, amount);

        // Ağ gecikmesi simülasyonu
        await Task.Delay(150, cancellationToken);

        // Örnek iş kuralı: 100.000 TL üzeri tutarlar banka limiti gereği reddedilir
        if (amount > 100000m)
        {
            return new BankSoapPaymentResponse
            {
                IsApproved = false,
                BankTransactionCode = string.Empty,
                AuthorizationCode = string.Empty,
                ResponseMessage = "DECLINED_LIMIT_EXCEEDED"
            };
        }

        return new BankSoapPaymentResponse
        {
            IsApproved = true,
            BankTransactionCode = $"SOAP_TXN_{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            AuthorizationCode = $"AUTH_{Random.Shared.Next(100000, 999999)}",
            ResponseMessage = "APPROVED"
        };
    }
}
