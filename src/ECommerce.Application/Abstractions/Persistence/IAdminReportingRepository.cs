namespace ECommerce.Application.Abstractions.Persistence;

public sealed record AdminDashboardSnapshot(
    int ActiveUsers,
    int ActiveProducts,
    int PendingOrders,
    int PaidOrders,
    int CancelledOrders,
    int RefundedOrders,
    int PendingPayments,
    int FailedPayments,
    int RefundedPayments,
    decimal PaidRevenue,
    decimal RefundedAmount);

public sealed record DailySalesSnapshot(
    DateOnly Date,
    int SuccessfulPayments,
    int RefundedPayments,
    decimal GrossRevenue,
    decimal RefundedAmount);

public sealed record TopSellingProductSnapshot(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal GrossRevenue);

public sealed record AdminSalesReportSnapshot(
    int OrdersCreated,
    int PaidOrders,
    int RefundedOrders,
    int SuccessfulPayments,
    int RefundedPayments,
    decimal GrossRevenue,
    decimal RefundedAmount,
    IReadOnlyCollection<DailySalesSnapshot> DailySales,
    IReadOnlyCollection<TopSellingProductSnapshot> TopProducts);

public interface IAdminReportingRepository
{
    Task<AdminDashboardSnapshot> GetDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<AdminSalesReportSnapshot> GetSalesReportAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int topProducts,
        CancellationToken cancellationToken = default);
}
