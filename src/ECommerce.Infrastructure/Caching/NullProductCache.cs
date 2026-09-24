using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Products.Dtos;

namespace ECommerce.Infrastructure.Caching;

public sealed class NullProductCache : IProductCache
{
    public Task<ProductDto?> GetAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult<ProductDto?>(null);

    public Task SetAsync(ProductDto product, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
