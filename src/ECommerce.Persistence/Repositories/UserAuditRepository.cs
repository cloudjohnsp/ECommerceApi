using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using ECommerce.Shared.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class UserAuditRepository(AppDbContext dbContext) : IUserAuditRepository
{
    public async Task AddAsync(
        UserAuditEntry entry,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserAuditEntries.AddAsync(entry, cancellationToken);

    public async Task<PagedResult<UserAuditEntry>> GetByUserIdAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.UserAuditEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId);
        var totalCount = await query.CountAsync(cancellationToken);
        var entries = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<UserAuditEntry>(entries, page, pageSize, totalCount);
    }
}
