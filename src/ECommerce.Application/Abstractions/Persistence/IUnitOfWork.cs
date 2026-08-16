namespace ECommerce.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> Commit(CancellationToken cancellationToken = default);
}
