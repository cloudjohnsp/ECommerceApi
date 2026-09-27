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

    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(payment => payment.OrderId == orderId, cancellationToken);

    public Task<Payment?> GetByOrderIdForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        dbContext.Payments
            .FromSqlInterpolated($"SELECT * FROM payments WHERE order_id = {orderId} FOR UPDATE")
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
