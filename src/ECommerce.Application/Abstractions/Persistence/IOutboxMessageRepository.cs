using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IOutboxMessageRepository
{
    Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    void Update(OutboxMessage message);
}
