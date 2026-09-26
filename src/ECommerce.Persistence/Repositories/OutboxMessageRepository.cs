using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class OutboxMessageRepository(AppDbContext dbContext) : IOutboxMessageRepository
{
    public Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.OutboxMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(message => message.Id == id, cancellationToken);

    public Task<OutboxMessage?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) => dbContext.Database.IsRelational()
            ? dbContext.OutboxMessages
                .FromSqlInterpolated($"SELECT * FROM \"OutboxMessages\" WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : dbContext.OutboxMessages.SingleOrDefaultAsync(message => message.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(
        IReadOnlyCollection<OutBoxMessageType> types,
        int take,
        CancellationToken cancellationToken = default) =>
        await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message => types.Contains(message.Type) && message.Status == OutBoxMessageStatus.Pending)
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
        await dbContext.OutboxMessages.AddAsync(message, cancellationToken);

    public void Update(OutboxMessage message) => dbContext.OutboxMessages.Update(message);
}
