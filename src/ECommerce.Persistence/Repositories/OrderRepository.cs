using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class OrderRepository(AppDbContext dbContext) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Orders.Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Order>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Orders.AsNoTracking().Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt).ToListAsync(cancellationToken);

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default) =>
        await dbContext.Orders.AddAsync(order, cancellationToken);

    public void Update(Order order) => dbContext.Orders.Update(order);
}
