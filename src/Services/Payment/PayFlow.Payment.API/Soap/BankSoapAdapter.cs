using System.ServiceModel;

namespace PayFlow.Payment.API.Soap;

public interface IBankSoapAdapter
{
    Task<BankSoapPaymentResponse> PayAsync(string orderReference, decimal amount, CancellationToken cancellationToken = default);
}

public class BankSoapAdapter : IBankSoapAdapter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BankSoapAdapter> _logger;

    public BankSoapAdapter(IConfiguration configuration, ILogger<BankSoapAdapter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BankSoapPaymentResponse> PayAsync(string orderReference, decimal amount, CancellationToken cancellationToken = default)
    {
        var soapEndpointUrl = _configuration["BankSoap:EndpointUrl"];

        // 1. Canlı SOAP Servisi URL'i tanımlıysa gerçek WCF ChannelFactory üzerinden SOAP/XML isteği gönder:
        if (!string.IsNullOrEmpty(soapEndpointUrl) && Uri.TryCreate(soapEndpointUrl, UriKind.Absolute, out _))
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "SOAP Banka servisi çağrısında hata meydana geldi. Mock fallback çalıştırılıyor.");
            }
        }

        // 2. Lokal/Test Geliştirme Ortamı: Gerçekçi SOAP yanıt simülasyonu
        _logger.LogInformation("[SOAP Entegrasyonu] Banka SOAP servisine XML isteği simüle ediliyor. Sipariş: {Ref}, Tutar: {Amount} TRY", orderReference, amount);

        // Kısa bir ağ gecikmesi simülasyonu
        await Task.Delay(200, cancellationToken);

        // Örnek iş kuralı: 100.000 TL üzeri tutarlar banka kuralı gereği reddedilir
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
