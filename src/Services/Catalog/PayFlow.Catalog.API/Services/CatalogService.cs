using Microsoft.EntityFrameworkCore;
using PayFlow.Catalog.API.Data;
using PayFlow.Catalog.API.DTOs;
using PayFlow.Catalog.API.Models;

namespace PayFlow.Catalog.API.Services;

/// <summary>
/// Katalog uygulama servisi: DB erişimi ve cache yönetimini koordine eder.
/// Endpoint'ler bu sınıfı kullanır; DB/cache detaylarını bilmez.
/// (Single Responsibility: DB + Cache orchestration buraya, HTTP routing endpoint'te)
/// </summary>
public sealed class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _db;
    private readonly ICatalogCacheService _cache;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(CatalogDbContext db, ICatalogCacheService cache, ILogger<CatalogService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<ProductDto> Products, string Source)> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        // Cache-Aside Pattern: Önce Redis kontrol et
        var cached = await _cache.GetProductsAsync(cancellationToken);
        if (cached is not null)
        {
            return (cached, "Redis Cache");
        }

        // Cache Miss: DB'den yüksek performanslı oku
        var products = await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.Category, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        // Arka planda cache'e yaz
        await _cache.SetProductsAsync(products, cancellationToken);

        _logger.LogInformation("Catalog ürünleri DB'den çekildi ve Redis'e yazıldı. Adet: {Count}", products.Count);
        return (products, "Database (EF Core)");
    }

    public async Task<(ProductDto? Product, string Source)> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetProductByIdAsync(id, cancellationToken);
        if (cached is not null)
        {
            return (cached, "Redis Cache");
        }

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null) return (null, string.Empty);

        var dto = ToDto(product);
        await _cache.SetProductByIdAsync(dto, cancellationToken);

        return (dto, "Database (EF Core)");
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _db.Products
            .AsNoTracking()
            .Where(p => p.Category.ToLower() == category.ToLower())
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.Category, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            Category = request.Category
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        // Cache Invalidation: Eski liste artık geçersiz
        await _cache.InvalidateCacheAsync(null, cancellationToken);

        _logger.LogInformation("Yeni ürün oluşturuldu: {ProductId} - {Name}", product.Id, product.Name);
        return ToDto(product);
    }

    public async Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FindAsync([id], cancellationToken);
        if (product is null) return null;

        product.Update(request.Name, request.Description, request.Price, request.StockQuantity, request.Category);
        await _db.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateCacheAsync(product.Id, cancellationToken);

        _logger.LogInformation("Ürün güncellendi: {ProductId}", product.Id);
        return ToDto(product);
    }

    public async Task PurgeCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cache.InvalidateCacheAsync(null, cancellationToken);
        _logger.LogInformation("Catalog Redis cache tamamen temizlendi.");
    }

    private static ProductDto ToDto(Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.Category, p.CreatedAtUtc);
}
