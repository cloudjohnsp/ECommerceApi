using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class ProductRepository(AppDbContext dbContext) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Products.Include(product => product.Inventory)
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

    public async Task<Product?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .FromSqlInterpolated($"SELECT * FROM products WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (product is not null)
            await dbContext.Entry(product).Reference(item => item.Inventory).LoadAsync(cancellationToken);
        return product;
    }

    public async Task<IReadOnlyCollection<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Products.AsNoTracking().Include(product => product.Inventory)
            .OrderBy(product => product.Name).ToListAsync(cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        await dbContext.Products.AddAsync(product, cancellationToken);

    public void Update(Product product) => dbContext.Products.Update(product);
}
