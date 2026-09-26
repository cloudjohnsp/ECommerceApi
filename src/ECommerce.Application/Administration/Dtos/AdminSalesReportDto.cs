namespace ECommerce.Application.Administration.Dtos;

public sealed record DailySalesDto(
    DateOnly Date,
    int SuccessfulPayments,
    int RefundedPayments,
    decimal GrossRevenue,
    decimal RefundedAmount,
    decimal NetRevenue);

public sealed record TopSellingProductDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal GrossRevenue);

public sealed record AdminSalesReportDto(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int OrdersCreated,
    int PaidOrders,
    int RefundedOrders,
    int SuccessfulPayments,
    int RefundedPayments,
    decimal GrossRevenue,
    decimal RefundedAmount,
    decimal NetRevenue,
    IReadOnlyCollection<DailySalesDto> DailySales,
    IReadOnlyCollection<TopSellingProductDto> TopProducts,
    DateTimeOffset GeneratedAtUtc);
