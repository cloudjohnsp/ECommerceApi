using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class ProductImageRepository(AppDbContext dbContext) : IProductImageRepository
{
    public async Task<IReadOnlyCollection<ProductImage>> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ProductImages.AsNoTracking()
            .Where(image => image.ProductId == productId)
            .OrderBy(image => image.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ProductImage image, CancellationToken cancellationToken = default) =>
        await dbContext.ProductImages.AddAsync(image, cancellationToken);
}
