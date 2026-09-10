using PayFlow.Catalog.API.DTOs;

namespace PayFlow.Catalog.API.Services;

public interface ICatalogCacheService
{
    Task<IReadOnlyList<ProductDto>?> GetProductsAsync(CancellationToken cancellationToken = default);
    Task SetProductsAsync(IReadOnlyList<ProductDto> products, CancellationToken cancellationToken = default);
    Task<ProductDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetProductByIdAsync(ProductDto product, CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(Guid? productId = null, CancellationToken cancellationToken = default);
}
