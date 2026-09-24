using ECommerce.Application.Products.Dtos;

namespace ECommerce.Application.Abstractions.Caching;

public interface IProductCache
{
    Task<ProductDto?> GetAsync(Guid productId, CancellationToken cancellationToken = default);
    Task SetAsync(ProductDto product, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid productId, CancellationToken cancellationToken = default);
}
