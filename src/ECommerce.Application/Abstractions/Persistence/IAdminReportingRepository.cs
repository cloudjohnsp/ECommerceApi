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

public interface IAdminReportingRepository
{
    Task<AdminDashboardSnapshot> GetDashboardAsync(
        CancellationToken cancellationToken = default);
}
