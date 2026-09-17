using Microsoft.EntityFrameworkCore;
using PayFlow.Catalog.API.Data;
using PayFlow.Catalog.API.DTOs;
using PayFlow.Catalog.API.Models;
using PayFlow.Catalog.API.Services;

namespace PayFlow.Catalog.API.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog");

        // 1. Tüm ürünleri listele (Redis Cache-Aside)
        group.MapGet("/", async (CatalogDbContext db, ICatalogCacheService cache, CancellationToken ct) =>
        {
            // Önce Redis kontrol edilir (Sub-millisecond response)
            var cached = await cache.GetProductsAsync(ct);
            if (cached is not null)
            {
                return Results.Ok(new { Source = "Redis Cache", Count = cached.Count, Data = cached });
            }

            // Cache Miss: Veritabanından AsNoTracking() ile yüksek performanslı okuma
            var products = await db.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.Category, p.CreatedAtUtc))
                .ToListAsync(ct);

            // Redis'e yazılır (arka planda)
            await cache.SetProductsAsync(products, ct);

            return Results.Ok(new { Source = "Database (EF Core)", Count = products.Count, Data = products });
        })
        .WithName("GetProducts")
        .WithSummary("Tüm ürünleri listeler (Redis önbellek destekli)");

        // 2. ID ile ürün getir (Redis Cache-Aside)
        group.MapGet("/{id:guid}", async (Guid id, CatalogDbContext db, ICatalogCacheService cache, CancellationToken ct) =>
        {
            var cached = await cache.GetProductByIdAsync(id, ct);
            if (cached is not null)
            {
                return Results.Ok(new { Source = "Redis Cache", Data = cached });
            }

            var product = await db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (product is null)
            {
                return Results.NotFound(new { Message = $"Product with id {id} not found." });
            }

            var dto = new ProductDto(product.Id, product.Name, product.Description, product.Price, product.StockQuantity, product.Category, product.CreatedAtUtc);
            await cache.SetProductByIdAsync(dto, ct);

            return Results.Ok(new { Source = "Database (EF Core)", Data = dto });
        })
        .WithName("GetProductById");

        // 3. Kategoriye göre ara (DB Index kullanımı)
        group.MapGet("/category/{category}", async (string category, CatalogDbContext db, CancellationToken ct) =>
        {
            var products = await db.Products
                .AsNoTracking()
                .Where(p => p.Category.ToLower() == category.ToLower())
                .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.Category, p.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(products);
        })
        .WithName("GetProductsByCategory");

        // 4. Yeni ürün ekle (Cache Invalidation)
        group.MapPost("/", async (CreateProductRequest request, CatalogDbContext db, ICatalogCacheService cache, CancellationToken ct) =>
        {
            var product = new Product
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
                Category = request.Category
            };

            db.Products.Add(product);
            await db.SaveChangesAsync(ct);

            // Cache invalidate edilir, böylece eski liste dönmez
            await cache.InvalidateCacheAsync(product.Id, ct);

            var dto = new ProductDto(product.Id, product.Name, product.Description, product.Price, product.StockQuantity, product.Category, product.CreatedAtUtc);
            return Results.Created($"/api/catalog/{product.Id}", dto);
        })
        .WithName("CreateProduct");

        // 5. Ürün güncelle (Cache Invalidation)
        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, CatalogDbContext db, ICatalogCacheService cache, CancellationToken ct) =>
        {
            var product = await db.Products.FindAsync([id], ct);
            if (product is null) return Results.NotFound();

            product.Update(request.Name, request.Description, request.Price, request.StockQuantity, request.Category);
            await db.SaveChangesAsync(ct);

            await cache.InvalidateCacheAsync(product.Id, ct);

            var dto = new ProductDto(product.Id, product.Name, product.Description, product.Price, product.StockQuantity, product.Category, product.CreatedAtUtc);
            return Results.Ok(dto);
        })
        .WithName("UpdateProduct");

        // 6. Redis Önbelleğini Temizle (Test ve Demo için)
        group.MapPost("/cache/purge", async (ICatalogCacheService cache, CancellationToken ct) =>
        {
            await cache.InvalidateCacheAsync(null, ct);
            return Results.Ok(new { Message = "Catalog cache purged successfully." });
        })
        .WithName("PurgeCatalogCache")
        .WithSummary("Redis önbelleğini temizler.");
    }
}
