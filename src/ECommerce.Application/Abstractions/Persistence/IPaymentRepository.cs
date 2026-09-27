using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Payment>> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
    Task<Payment?> GetByOrderAndIdempotencyKeyAsync(
        Guid orderId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    Task<Payment?> GetPendingByOrderIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
    Task<Payment?> GetPaidByOrderIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
    Task<Payment?> GetPaidOrRefundedByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
    Task<Payment?> GetPaidOrRefundedByOrderIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
    Task<Payment?> GetByExternalIdForUpdateAsync(string externalPaymentId, CancellationToken cancellationToken = default);
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);
    void Update(Payment payment);
}
