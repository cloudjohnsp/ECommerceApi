using System.Text.Json;
using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Products.Dtos;
using ECommerce.Infrastructure.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Caching;

public sealed class RedisProductCache(
    IDistributedCache cache,
    IOptions<RedisOptions> options,
    ILogger<RedisProductCache> logger) : IProductCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeSpan _expiration = TimeSpan.FromMinutes(options.Value.ProductExpirationMinutes);

    public async Task<ProductDto?> GetAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await cache.GetStringAsync(GetKey(productId), cancellationToken);
            return json is null
                ? null
                : JsonSerializer.Deserialize<ProductDto>(json, SerializerOptions);
        }
        catch (Exception exception) when (!IsRequestedCancellation(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Failed to read product {ProductId} from Redis.", productId);
            return null;
        }
    }

    public async Task SetAsync(ProductDto product, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(product, SerializerOptions);
            await cache.SetStringAsync(
                GetKey(product.Id),
                json,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _expiration },
                cancellationToken);
        }
        catch (Exception exception) when (!IsRequestedCancellation(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Failed to cache product {ProductId} in Redis.", product.Id);
        }
    }

    public async Task RemoveAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(GetKey(productId), cancellationToken);
        }
        catch (Exception exception) when (!IsRequestedCancellation(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Failed to invalidate product {ProductId} in Redis.", productId);
        }
    }

    private static string GetKey(Guid productId) => $"products:{productId:N}";

    private static bool IsRequestedCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
}
