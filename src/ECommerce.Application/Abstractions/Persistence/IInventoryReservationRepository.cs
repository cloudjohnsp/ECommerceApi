using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IInventoryReservationRepository
{
    Task<IReadOnlyCollection<InventoryReservation>> GetActiveByOrderIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        InventoryReservation reservation,
        CancellationToken cancellationToken = default);

    void Update(InventoryReservation reservation);
}
