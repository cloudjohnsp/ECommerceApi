using System.Text.Json;
using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Categories.Dtos;
using ECommerce.Infrastructure.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Caching;

public sealed class RedisCategoryCache(
    IDistributedCache cache,
    IOptions<RedisOptions> options,
    ILogger<RedisCategoryCache> logger) : ICategoryCache
{
    private const string AllCategoriesKey = "categories:all";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeSpan _expiration = TimeSpan.FromMinutes(options.Value.CategoryExpirationMinutes);

    public async Task<CategoryDto?> GetAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await cache.GetStringAsync(GetKey(categoryId), cancellationToken);
            return json is null
                ? null
                : JsonSerializer.Deserialize<CategoryDto>(json, SerializerOptions);
        }
        catch (Exception exception) when (!IsRequestedCancellation(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Failed to read category {CategoryId} from Redis.", categoryId);
            return null;
        }
    }

    public async Task<IReadOnlyCollection<CategoryDto>?> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await cache.GetStringAsync(AllCategoriesKey, cancellationToken);
            return json is null
                ? null
                : JsonSerializer.Deserialize<CategoryDto[]>(json, SerializerOptions);
        }
        catch (Exception exception) when (!IsRequestedCancellation(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Failed to read the category list from Redis.");
            return null;
        }
    }

    public Task SetAsync(CategoryDto category, CancellationToken cancellationToken = default) =>
        SetValueAsync(GetKey(category.Id), category, cancellationToken);

    public Task SetAllAsync(
        IReadOnlyCollection<CategoryDto> categories,
        CancellationToken cancellationToken = default) =>
        SetValueAsync(AllCategoriesKey, categories, cancellationToken);

    public async Task RemoveAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(GetKey(categoryId), cancellationToken);
        }
        catch (Exception exception) when (!IsRequestedCancellation(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Failed to invalidate category {CategoryId} in Redis.", categoryId);
        }
    }

    public async Task RemoveAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(AllCategoriesKey, cancellationToken);
        }
        catch (Exception exception) when (!IsRequestedCancellation(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Failed to invalidate the category list in Redis.");
        }
    }

    private async Task SetValueAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, SerializerOptions);
            await cache.SetStringAsync(
                key,
                json,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _expiration },
                cancellationToken);
        }
        catch (Exception exception) when (!IsRequestedCancellation(exception, cancellationToken))
        {
            logger.LogWarning(exception, "Failed to cache category data under key {CacheKey} in Redis.", key);
        }
    }

    private static string GetKey(Guid categoryId) => $"categories:{categoryId:N}";

    private static bool IsRequestedCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
}
