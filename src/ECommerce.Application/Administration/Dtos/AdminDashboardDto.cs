namespace ECommerce.Application.Administration.Dtos;

public sealed record AdminDashboardDto(
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
    decimal RefundedAmount,
    DateTimeOffset GeneratedAtUtc);
