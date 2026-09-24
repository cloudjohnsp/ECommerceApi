using ECommerce.Application.Categories.Dtos;
using ECommerce.Infrastructure.Caching;
using ECommerce.Infrastructure.Options;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ECommerce.Infrastructure.Tests.Caching;

public sealed class RedisCategoryCacheTests
{
    [Fact]
    public async Task SetAndGet_RoundTripsCategoryDto()
    {
        var cache = CreateCache(CreateDistributedCache());
        var category = CreateCategory("Audio");

        await cache.SetAsync(category);
        var cached = await cache.GetAsync(category.Id);

        cached.Should().BeEquivalentTo(category);
    }

    [Fact]
    public async Task SetAllAndGetAll_RoundTripsAnEmptyCollection()
    {
        var cache = CreateCache(CreateDistributedCache());
        IReadOnlyCollection<CategoryDto> categories = [];

        await cache.SetAllAsync(categories);
        var cached = await cache.GetAllAsync();

        cached.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Remove_DeletesCachedCategory()
    {
        var cache = CreateCache(CreateDistributedCache());
        var category = CreateCategory("Audio");
        await cache.SetAsync(category);

        await cache.RemoveAsync(category.Id);

        (await cache.GetAsync(category.Id)).Should().BeNull();
    }

    [Fact]
    public async Task RemoveAll_DeletesCachedCategoryList()
    {
        var cache = CreateCache(CreateDistributedCache());
        await cache.SetAllAsync([CreateCategory("Audio")]);

        await cache.RemoveAllAsync();

        (await cache.GetAllAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Get_WhenRedisFails_ReturnsCacheMiss()
    {
        var distributedCache = new Mock<IDistributedCache>();
        distributedCache
            .Setup(cache => cache.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));
        var cache = CreateCache(distributedCache.Object);

        var category = await cache.GetAsync(Guid.NewGuid());
        var categories = await cache.GetAllAsync();

        category.Should().BeNull();
        categories.Should().BeNull();
    }

    private static IDistributedCache CreateDistributedCache() =>
        new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

    private static RedisCategoryCache CreateCache(IDistributedCache distributedCache) => new(
        distributedCache,
        Microsoft.Extensions.Options.Options.Create(
            new RedisOptions { CategoryExpirationMinutes = 10 }),
        NullLogger<RedisCategoryCache>.Instance);

    private static CategoryDto CreateCategory(string name) => new(
        Guid.NewGuid(), name, name.ToLowerInvariant(), true,
        DateTimeOffset.UtcNow, null);
}
