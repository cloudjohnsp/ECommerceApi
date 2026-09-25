using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Enums;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class AdminReportingRepository(AppDbContext dbContext) : IAdminReportingRepository
{
    public async Task<AdminDashboardSnapshot> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var activeUsers = await dbContext.Users.AsNoTracking()
            .CountAsync(user => user.IsActive, cancellationToken);
        var activeProducts = await dbContext.Products.AsNoTracking()
            .CountAsync(product => product.IsActive, cancellationToken);
        var pendingOrders = await dbContext.Orders.AsNoTracking()
            .CountAsync(order => order.Status == OrderStatus.Pending, cancellationToken);
        var paidOrders = await dbContext.Orders.AsNoTracking()
            .CountAsync(order => order.Status == OrderStatus.Paid, cancellationToken);
        var cancelledOrders = await dbContext.Orders.AsNoTracking()
            .CountAsync(order => order.Status == OrderStatus.Cancelled, cancellationToken);
        var pendingPayments = await dbContext.Payments.AsNoTracking()
            .CountAsync(payment => payment.Status == PaymentStatus.Pending, cancellationToken);
        var failedPayments = await dbContext.Payments.AsNoTracking()
            .CountAsync(payment => payment.Status == PaymentStatus.Failed, cancellationToken);
        var paidRevenue = await dbContext.Payments.AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Paid)
            .SumAsync(payment => payment.Amount, cancellationToken);

        return new AdminDashboardSnapshot(
            activeUsers,
            activeProducts,
            pendingOrders,
            paidOrders,
            cancelledOrders,
            pendingPayments,
            failedPayments,
            paidRevenue);
    }
}
