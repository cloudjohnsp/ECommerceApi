using ECommerce.Domain.Entities;
using ECommerce.Shared.Pagination;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IUserAuditRepository
{
    Task AddAsync(UserAuditEntry entry, CancellationToken cancellationToken = default);
    Task<PagedResult<UserAuditEntry>> GetByUserIdAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
