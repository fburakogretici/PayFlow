using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PayFlow.Catalog.API.DTOs;

namespace PayFlow.Catalog.API.Services;

public class CatalogCacheService : ICatalogCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CatalogCacheService> _logger;

    private const string AllProductsKey = "catalog:products:all";
    private static string ProductKey(Guid id) => $"catalog:product:{id}";

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        SlidingExpiration = TimeSpan.FromMinutes(2)
    };

    public CatalogCacheService(IDistributedCache cache, ILogger<CatalogCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductDto>?> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await _cache.GetStringAsync(AllProductsKey, cancellationToken);
            if (string.IsNullOrEmpty(data)) return null;

            return JsonSerializer.Deserialize<List<ProductDto>>(data);
        }
        catch (Exception ex)
        {
            // Senior Resilience Pattern: Redis erişilemezse uygulamanın çökmesini engelle (Graceful Degradation)
            _logger.LogWarning(ex, "Redis cache read failed for key {Key}. Falling back to database.", AllProductsKey);
            return null;
        }
    }

    public async Task SetProductsAsync(IReadOnlyList<ProductDto> products, CancellationToken cancellationToken = default)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(products);
            await _cache.SetStringAsync(AllProductsKey, serialized, CacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache write failed for key {Key}.", AllProductsKey);
        }
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = ProductKey(id);
            var data = await _cache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(data)) return null;

            return JsonSerializer.Deserialize<ProductDto>(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache read failed for key {Key}.", ProductKey(id));
            return null;
        }
    }

    public async Task SetProductByIdAsync(ProductDto product, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = ProductKey(product.Id);
            var serialized = JsonSerializer.Serialize(product);
            await _cache.SetStringAsync(key, serialized, CacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache write failed for key {Key}.", ProductKey(product.Id));
        }
    }

    public async Task InvalidateCacheAsync(Guid? productId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(AllProductsKey, cancellationToken);
            if (productId.HasValue)
            {
                await _cache.RemoveAsync(ProductKey(productId.Value), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache eviction failed.");
        }
    }
}
