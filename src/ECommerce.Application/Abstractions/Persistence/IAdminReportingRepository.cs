namespace ECommerce.Application.Abstractions.Persistence;

public sealed record AdminDashboardSnapshot(
    int ActiveUsers,
    int ActiveProducts,
    int PendingOrders,
    int PaidOrders,
    int CancelledOrders,
    int PendingPayments,
    int FailedPayments,
    decimal PaidRevenue);

public interface IAdminReportingRepository
{
    Task<AdminDashboardSnapshot> GetDashboardAsync(
        CancellationToken cancellationToken = default);
}
