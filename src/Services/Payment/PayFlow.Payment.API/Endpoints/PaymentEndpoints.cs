using Microsoft.EntityFrameworkCore;
using PayFlow.Payment.API.Data;
using PayFlow.Payment.API.Soap;

namespace PayFlow.Payment.API.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments");

        // 1. Sipariş ID'sine göre ödeme kaydını getir
        group.MapGet("/order/{orderId:guid}", async (Guid orderId, PaymentDbContext db, CancellationToken ct) =>
        {
            var payment = await db.PaymentRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

            return payment is not null
                ? Results.Ok(payment)
                : Results.NotFound(new { Message = $"Payment for order {orderId} not found." });
        })
        .WithName("GetPaymentByOrderId")
        .WithSummary("Sipariş ID'sine ait ödeme sonucunu döner.");

        // 2. Tüm ödeme işlemlerini listele
        group.MapGet("/", async (PaymentDbContext db, CancellationToken ct) =>
        {
            var payments = await db.PaymentRecords
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAtUtc)
                .ToListAsync(ct);

            return Results.Ok(payments);
        })
        .WithName("GetAllPayments");

        // 3. SOAP Servis Entegrasyon Test Uç Noktası (Swagger'dan manuel test imkanı)
        group.MapPost("/soap-test", async (string orderReference, decimal amount, IBankSoapAdapter soapAdapter, CancellationToken ct) =>
        {
            var response = await soapAdapter.PayAsync(orderReference, amount, ct);
            return Results.Ok(new
            {
                Description = "SOAP / WCF Banka Servis Yanıtı",
                Response = response
            });
        })
        .WithName("TestSoapBankService")
        .WithSummary("SOAP Banka servisi entegrasyonunu doğrudan test eder.");
    }
}
