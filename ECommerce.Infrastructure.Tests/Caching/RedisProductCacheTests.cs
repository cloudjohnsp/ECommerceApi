using ECommerce.Application.Products.Dtos;
using ECommerce.Infrastructure.Caching;
using ECommerce.Infrastructure.Options;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.Infrastructure.Tests.Caching;

public sealed class RedisProductCacheTests
{
    [Fact]
    public async Task SetAndGet_RoundTripsProductDto()
    {
        var distributedCache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var cache = CreateCache(distributedCache);
        var product = CreateProduct();

        await cache.SetAsync(product);
        var cached = await cache.GetAsync(product.Id);

        cached.Should().BeEquivalentTo(product);
    }

    [Fact]
    public async Task Remove_DeletesCachedProduct()
    {
        var distributedCache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var cache = CreateCache(distributedCache);
        var product = CreateProduct();
        await cache.SetAsync(product);

        await cache.RemoveAsync(product.Id);

        (await cache.GetAsync(product.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Get_WhenRedisFails_ReturnsCacheMiss()
    {
        var distributedCache = new Mock<IDistributedCache>();
        distributedCache
            .Setup(cache => cache.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));
        var cache = CreateCache(distributedCache.Object);

        var result = await cache.GetAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    private static RedisProductCache CreateCache(IDistributedCache distributedCache) => new(
        distributedCache,
        Microsoft.Extensions.Options.Options.Create(
            new RedisOptions { ProductExpirationMinutes = 5 }),
        NullLogger<RedisProductCache>.Instance);

    private static ProductDto CreateProduct() => new(
        Guid.NewGuid(), "Mouse", "Wireless", 150m, 8, true,
        DateTimeOffset.UtcNow, null);
}
