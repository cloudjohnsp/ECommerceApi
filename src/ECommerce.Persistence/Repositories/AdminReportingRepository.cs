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
        var refundedOrders = await dbContext.Orders.AsNoTracking()
            .CountAsync(order => order.Status == OrderStatus.Refunded, cancellationToken);
        var pendingPayments = await dbContext.Payments.AsNoTracking()
            .CountAsync(payment => payment.Status == PaymentStatus.Pending, cancellationToken);
        var failedPayments = await dbContext.Payments.AsNoTracking()
            .CountAsync(payment => payment.Status == PaymentStatus.Failed, cancellationToken);
        var refundedPayments = await dbContext.Payments.AsNoTracking()
            .CountAsync(payment => payment.Status == PaymentStatus.Refunded, cancellationToken);
        var paidRevenue = await dbContext.Payments.AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Paid)
            .SumAsync(payment => payment.Amount, cancellationToken);
        var refundedAmount = await dbContext.Payments.AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Refunded)
            .SumAsync(payment => payment.Amount, cancellationToken);

        return new AdminDashboardSnapshot(
            activeUsers,
            activeProducts,
            pendingOrders,
            paidOrders,
            cancelledOrders,
            refundedOrders,
            pendingPayments,
            failedPayments,
            refundedPayments,
            paidRevenue,
            refundedAmount);
    }

    public async Task<AdminSalesReportSnapshot> GetSalesReportAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int topProducts,
        CancellationToken cancellationToken = default)
    {
        var ordersCreated = await dbContext.Orders.AsNoTracking()
            .CountAsync(order => order.CreatedAt >= fromUtc && order.CreatedAt < toUtc, cancellationToken);

        var successfulPaymentsQuery = dbContext.Payments.AsNoTracking()
            .Where(payment => payment.PaidAt >= fromUtc && payment.PaidAt < toUtc);
        var refundedPaymentsQuery = dbContext.Payments.AsNoTracking()
            .Where(payment => payment.RefundedAt >= fromUtc && payment.RefundedAt < toUtc);

        var successfulPayments = await successfulPaymentsQuery.CountAsync(cancellationToken);
        var refundedPayments = await refundedPaymentsQuery.CountAsync(cancellationToken);
        var paidOrders = await successfulPaymentsQuery
            .Select(payment => payment.OrderId)
            .Distinct()
            .CountAsync(cancellationToken);
        var refundedOrders = await refundedPaymentsQuery
            .Select(payment => payment.OrderId)
            .Distinct()
            .CountAsync(cancellationToken);
        var grossRevenue = await successfulPaymentsQuery
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
        var refundedAmount = await refundedPaymentsQuery
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;

        var grossByDay = await successfulPaymentsQuery
            .GroupBy(payment => payment.PaidAt!.Value.Date)
            .Select(group => new
            {
                Date = group.Key,
                Count = group.Count(),
                Amount = group.Sum(payment => payment.Amount)
            })
            .ToListAsync(cancellationToken);
        var refundsByDay = await refundedPaymentsQuery
            .GroupBy(payment => payment.RefundedAt!.Value.Date)
            .Select(group => new
            {
                Date = group.Key,
                Count = group.Count(),
                Amount = group.Sum(payment => payment.Amount)
            })
            .ToListAsync(cancellationToken);

        var grossLookup = grossByDay.ToDictionary(item => DateOnly.FromDateTime(item.Date));
        var refundLookup = refundsByDay.ToDictionary(item => DateOnly.FromDateTime(item.Date));
        var dailySales = grossLookup.Keys
            .Union(refundLookup.Keys)
            .Order()
            .Select(date =>
            {
                grossLookup.TryGetValue(date, out var gross);
                refundLookup.TryGetValue(date, out var refund);
                return new DailySalesSnapshot(
                    date,
                    gross?.Count ?? 0,
                    refund?.Count ?? 0,
                    gross?.Amount ?? 0m,
                    refund?.Amount ?? 0m);
            })
            .ToArray();

        var paidOrderIds = successfulPaymentsQuery.Select(payment => payment.OrderId);
        var topSellingProductRows = await dbContext.OrderItems.AsNoTracking()
            .Where(item => paidOrderIds.Contains(item.OrderId))
            .GroupBy(item => new { item.ProductId, item.ProductName })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.ProductName,
                Quantity = group.Sum(item => item.Quantity),
                GrossRevenue = group.Sum(item => item.UnitPrice * item.Quantity)
            })
            .OrderByDescending(product => product.Quantity)
            .ThenByDescending(product => product.GrossRevenue)
            .ThenBy(product => product.ProductName)
            .Take(topProducts)
            .ToListAsync(cancellationToken);
        var topSellingProducts = topSellingProductRows
            .Select(product => new TopSellingProductSnapshot(
                product.ProductId,
                product.ProductName,
                product.Quantity,
                product.GrossRevenue))
            .ToArray();

        return new AdminSalesReportSnapshot(
            ordersCreated,
            paidOrders,
            refundedOrders,
            successfulPayments,
            refundedPayments,
            grossRevenue,
            refundedAmount,
            dailySales,
            topSellingProducts);
    }
}
