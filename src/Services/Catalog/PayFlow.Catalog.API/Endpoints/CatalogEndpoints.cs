using PayFlow.Catalog.API.DTOs;
using PayFlow.Catalog.API.Services;

namespace PayFlow.Catalog.API.Endpoints;

/// <summary>
/// Endpoint'ler yalnızca HTTP routing ve istek/yanıt dönüşümü yapar.
/// Tüm iş mantığı ICatalogService'e devredilmiştir.
/// (Single Responsibility Principle)
/// </summary>
public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog");

        // 1. Tüm ürünleri listele (Redis Cache-Aside)
        group.MapGet("/", async (ICatalogService catalog, CancellationToken ct) =>
        {
            var (products, source) = await catalog.GetAllProductsAsync(ct);
            return Results.Ok(new { Source = source, Count = products.Count, Data = products });
        })
        .WithName("GetProducts")
        .WithSummary("Tüm ürünleri listeler (Redis önbellek destekli)");

        // 2. ID ile ürün getir (Redis Cache-Aside)
        group.MapGet("/{id:guid}", async (Guid id, ICatalogService catalog, CancellationToken ct) =>
        {
            var (product, source) = await catalog.GetProductByIdAsync(id, ct);
            if (product is null)
                return Results.NotFound(new { Message = $"Product with id {id} not found." });

            return Results.Ok(new { Source = source, Data = product });
        })
        .WithName("GetProductById");

        // 3. Kategoriye göre ara (DB Index kullanımı)
        group.MapGet("/category/{category}", async (string category, ICatalogService catalog, CancellationToken ct) =>
        {
            var products = await catalog.GetProductsByCategoryAsync(category, ct);
            return Results.Ok(products);
        })
        .WithName("GetProductsByCategory");

        // 4. Yeni ürün ekle (Cache Invalidation)
        group.MapPost("/", async (CreateProductRequest request, ICatalogService catalog, CancellationToken ct) =>
        {
            var dto = await catalog.CreateProductAsync(request, ct);
            return Results.Created($"/api/catalog/{dto.Id}", dto);
        })
        .WithName("CreateProduct");

        // 5. Ürün güncelle (Cache Invalidation)
        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, ICatalogService catalog, CancellationToken ct) =>
        {
            var dto = await catalog.UpdateProductAsync(id, request, ct);
            if (dto is null) return Results.NotFound();
            return Results.Ok(dto);
        })
        .WithName("UpdateProduct");

        // 6. Redis Önbelleğini Temizle (Test ve Demo için)
        group.MapPost("/cache/purge", async (ICatalogService catalog, CancellationToken ct) =>
        {
            await catalog.PurgeCacheAsync(ct);
            return Results.Ok(new { Message = "Catalog cache purged successfully." });
        })
        .WithName("PurgeCatalogCache")
        .WithSummary("Redis önbelleğini temizler.");
    }
}
