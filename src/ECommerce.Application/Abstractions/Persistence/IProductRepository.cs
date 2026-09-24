using ECommerce.Domain.Entities;
using ECommerce.Application.Abstractions.Specifications;
using ECommerce.Shared.Pagination;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<Product>> SearchAsync(
        ISpecification<Product> specification,
        CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    void Update(Product product);
}
