using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PayFlow.Catalog.API.DTOs;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace PayFlow.Catalog.API.Services;

public class CatalogCacheService : ICatalogCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CatalogCacheService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

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

        // Polly v8 Resilience Pipeline: Redis erişimlerinde hızlı zaman aşımı ve kısa süreli retry
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromMilliseconds(800)
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromMilliseconds(50),
                OnRetry = args =>
                {
                    logger.LogWarning("Redis işleminde hata oluştu ({Attempt}. deneme): {Message}", args.AttemptNumber, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<IReadOnlyList<ProductDto>?> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var data = await _cache.GetStringAsync(AllProductsKey, ct);
                if (string.IsNullOrEmpty(data)) return null;

                return JsonSerializer.Deserialize<List<ProductDto>>(data);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // Senior Resilience Pattern: Redis arızalansa bile sistem çökmez, veritabanına sorunsuz fallback yapar (Graceful Degradation)
            _logger.LogWarning(ex, "Redis cache read failed for key {Key}. Falling back to database.", AllProductsKey);
            return null;
        }
    }

    public async Task SetProductsAsync(IReadOnlyList<ProductDto> products, CancellationToken cancellationToken = default)
    {
        try
        {
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var serialized = JsonSerializer.Serialize(products);
                await _cache.SetStringAsync(AllProductsKey, serialized, CacheOptions, ct);
            }, cancellationToken);
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
            return await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var key = ProductKey(id);
                var data = await _cache.GetStringAsync(key, ct);
                if (string.IsNullOrEmpty(data)) return null;

                return JsonSerializer.Deserialize<ProductDto>(data);
            }, cancellationToken);
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
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var key = ProductKey(product.Id);
                var serialized = JsonSerializer.Serialize(product);
                await _cache.SetStringAsync(key, serialized, CacheOptions, ct);
            }, cancellationToken);
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
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                await _cache.RemoveAsync(AllProductsKey, ct);
                if (productId.HasValue)
                {
                    await _cache.RemoveAsync(ProductKey(productId.Value), ct);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache eviction failed.");
        }
    }
}
