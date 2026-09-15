namespace ECommerce.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> Commit(CancellationToken cancellationToken = default);

    // Transaction management methods
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
