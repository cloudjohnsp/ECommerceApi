using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IOutboxMessageRepository
{
    Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(
        IReadOnlyCollection<OutBoxMessageType> types,
        int take,
        CancellationToken cancellationToken = default);
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    void Update(OutboxMessage message);
}
