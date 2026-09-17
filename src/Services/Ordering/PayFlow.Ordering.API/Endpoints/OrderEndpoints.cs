using MediatR;
using PayFlow.Ordering.API.Application.Orders.Commands.CreateOrder;
using PayFlow.Ordering.API.Application.Orders.Queries.GetOrderById;
using PayFlow.Ordering.API.Application.Orders.Queries.GetOrders;
using PayFlow.SharedKernel.Results;

namespace PayFlow.Ordering.API.Endpoints;

/// <summary>
/// Endpoint'ler yalnızca HTTP routing ve istek/yanıt dönüşümü yapar.
/// Hata → HTTP mapping'i ToHttpResult() extension'ı aracılığıyla otomatik yapılır.
/// (Single Responsibility Principle + DRY)
/// </summary>
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        // 1. Yeni sipariş oluştur (CQRS Command & Transactional Outbox & JWT Korumalı)
        group.MapPost("/", async (CreateOrderCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            if (result.IsFailure)
                return result.Error.ToHttpResult();

            return Results.Created($"/api/orders/{result.Value}", new { OrderId = result.Value });
        })
        .RequireAuthorization()
        .WithName("CreateOrder")
        .WithSummary("Yeni sipariş oluşturur ve Outbox tablosu üzerinden RabbitMQ'ya event fırlatır (JWT Yetkisi gerektirir).");

        // 2. ID ile sipariş detayını getir (CQRS Query)
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetOrderByIdQuery(id), ct);
            return result.ToHttpResult();  // NotFound/BadRequest/OK otomatik
        })
        .WithName("GetOrderById")
        .WithSummary("Sipariş detayını ve durumunu getirir.");

        // 3. Siparişleri listele (Müşteriye göre filtrelenebilir)
        group.MapGet("/", async (Guid? customerId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetOrdersQuery(customerId), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.Error.ToHttpResult();
        })
        .WithName("GetOrders")
        .WithSummary("Siparişleri listeler.");
    }
}
