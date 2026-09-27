using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class PaymentRepository(AppDbContext dbContext) : IPaymentRepository
{
    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(payment => payment.Id == id, cancellationToken);

    public Task<Payment?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default) => dbContext.Database.IsRelational()
            ? dbContext.Payments
                .FromSqlInterpolated($"SELECT * FROM payments WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : dbContext.Payments.SingleOrDefaultAsync(payment => payment.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Payment>> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.OrderId == orderId)
            .OrderByDescending(payment => payment.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Payment?> GetByOrderAndIdempotencyKeyAsync(
        Guid orderId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        dbContext.Payments.FirstOrDefaultAsync(
            payment => payment.OrderId == orderId &&
                       payment.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public Task<Payment?> GetPendingByOrderIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        dbContext.Payments
            .FromSqlInterpolated($"SELECT * FROM payments WHERE order_id = {orderId} AND status = 1 FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Payment?> GetPaidByOrderIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        dbContext.Payments
            .FromSqlInterpolated($"SELECT * FROM payments WHERE order_id = {orderId} AND status = 2 FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Payment?> GetPaidOrRefundedByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        dbContext.Payments.AsNoTracking().SingleOrDefaultAsync(
            payment => payment.OrderId == orderId &&
                       (payment.Status == Domain.Enums.PaymentStatus.Paid ||
                        payment.Status == Domain.Enums.PaymentStatus.Refunded),
            cancellationToken);

    public Task<Payment?> GetPaidOrRefundedByOrderIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        dbContext.Payments
            .FromSqlInterpolated($"SELECT * FROM payments WHERE order_id = {orderId} AND status IN (2, 4) FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Payment?> GetByExternalIdForUpdateAsync(
        string externalPaymentId,
        CancellationToken cancellationToken = default) =>
        dbContext.Payments
            .FromSqlInterpolated($"SELECT * FROM payments WHERE external_payment_id = {externalPaymentId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default) =>
        await dbContext.Payments.AddAsync(payment, cancellationToken);

    public void Update(Payment payment) => dbContext.Payments.Update(payment);
}
