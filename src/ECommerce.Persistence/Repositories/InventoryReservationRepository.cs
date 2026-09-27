using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class InventoryReservationRepository(AppDbContext dbContext)
    : IInventoryReservationRepository
{
    public async Task<IReadOnlyCollection<InventoryReservation>> GetActiveByOrderIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        await dbContext.InventoryReservations
            .FromSqlInterpolated($$"""
                SELECT *
                FROM inventory_reservations
                WHERE order_id = {{orderId}} AND status = 1
                ORDER BY product_id
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        InventoryReservation reservation,
        CancellationToken cancellationToken = default) =>
        await dbContext.InventoryReservations.AddAsync(reservation, cancellationToken);

    public void Update(InventoryReservation reservation) =>
        dbContext.InventoryReservations.Update(reservation);
}
