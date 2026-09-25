using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IProductImageRepository
{
    Task<IReadOnlyCollection<ProductImage>> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
    Task AddAsync(ProductImage image, CancellationToken cancellationToken = default);
}
