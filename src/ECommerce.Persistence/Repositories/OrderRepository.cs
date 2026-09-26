using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using ECommerce.Application.Abstractions.Specifications;
using ECommerce.Shared.Pagination;

namespace ECommerce.Persistence.Repositories;

public sealed class OrderRepository(AppDbContext dbContext) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Orders.Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public Task<Order?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Orders
            .FromSqlInterpolated($"SELECT * FROM orders WHERE \"Id\" = {id} FOR UPDATE")
            .Include(order => order.Items)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Order>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Orders.AsNoTracking().Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Order>> GetByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Orders.AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<Order>> SearchAsync(
        ISpecification<Order> specification,
        CancellationToken cancellationToken = default)
    {
        var filteredQuery = dbContext.Orders.AsNoTracking().Where(specification.Criteria);
        var totalCount = await filteredQuery.CountAsync(cancellationToken);
        var items = await specification.ApplyOrdering(filteredQuery)
            .Include(order => order.Items)
            .Skip(specification.Skip)
            .Take(specification.Take)
            .ToListAsync(cancellationToken);
        return new PagedResult<Order>(
            items,
            specification.Skip / specification.Take + 1,
            specification.Take,
            totalCount);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default) =>
        await dbContext.Orders.AddAsync(order, cancellationToken);

    public void Update(Order order) => dbContext.Orders.Update(order);
}
