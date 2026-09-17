using PayFlow.Catalog.API.DTOs;

namespace PayFlow.Catalog.API.Services;

/// <summary>
/// Katalog uygulama servisi arayüzü.
/// Endpoint'ler sadece bu arayüzü bilir; DB veya cache detaylarından habersizdir.
/// (Single Responsibility Principle – Endpoints sadece routing/HTTP sorumluluğuna sahip)
/// </summary>
public interface ICatalogService
{
    /// <summary>Tüm ürünleri getirir. Kaynak bilgisini (Cache/DB) de döndürür.</summary>
    Task<(IReadOnlyList<ProductDto> Products, string Source)> GetAllProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>ID ile tek ürün getirir. Kaynak bilgisini de döndürür.</summary>
    Task<(ProductDto? Product, string Source)> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Kategoriye göre ürün listesi getirir.</summary>
    Task<IReadOnlyList<ProductDto>> GetProductsByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>Yeni ürün oluşturur.</summary>
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>Mevcut ürünü günceller.</summary>
    Task<ProductDto?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>Redis önbelleğini temizler.</summary>
    Task PurgeCacheAsync(CancellationToken cancellationToken = default);
}
