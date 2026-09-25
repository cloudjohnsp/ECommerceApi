namespace ECommerce.Application.Administration.Dtos;

public sealed record AdminDashboardDto(
    int ActiveUsers,
    int ActiveProducts,
    int PendingOrders,
    int PaidOrders,
    int CancelledOrders,
    int PendingPayments,
    int FailedPayments,
    decimal PaidRevenue,
    DateTimeOffset GeneratedAtUtc);
