using System.Runtime.Serialization;
using System.ServiceModel;

namespace PayFlow.Payment.API.Soap;

[DataContract(Namespace = "http://payflow.bank.legacy/soap/v1")]
public class BankSoapPaymentRequest
{
    [DataMember(Order = 1)]
    public string MerchantId { get; set; } = string.Empty;

    [DataMember(Order = 2)]
    public string OrderReference { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public decimal Amount { get; set; }

    [DataMember(Order = 4)]
    public string Currency { get; set; } = "TRY";
}

[DataContract(Namespace = "http://payflow.bank.legacy/soap/v1")]
public class BankSoapPaymentResponse
{
    [DataMember(Order = 1)]
    public bool IsApproved { get; set; }

    [DataMember(Order = 2)]
    public string BankTransactionCode { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public string ResponseMessage { get; set; } = string.Empty;

    [DataMember(Order = 4)]
    public string AuthorizationCode { get; set; } = string.Empty;
}

// Senior İlan Gereksinimi: SOAP / WCF Tabanlı Servis Entegrasyonu Kontratı
[ServiceContract(Namespace = "http://payflow.bank.legacy/soap/v1", Name = "IBankSoapService")]
public interface IBankSoapService
{
    [OperationContract]
    Task<BankSoapPaymentResponse> ProcessPaymentAsync(BankSoapPaymentRequest request);
}
