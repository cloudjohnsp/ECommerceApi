using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class OutboxMessageRepository(AppDbContext dbContext) : IOutboxMessageRepository
{
    public Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.OutboxMessages.FirstOrDefaultAsync(message => message.Id == id, cancellationToken);

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
        await dbContext.OutboxMessages.AddAsync(message, cancellationToken);

    public void Update(OutboxMessage message) => dbContext.OutboxMessages.Update(message);
}
