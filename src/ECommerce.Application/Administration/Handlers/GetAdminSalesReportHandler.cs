using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Administration.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Administration.Handlers;

public sealed class GetAdminSalesReportHandler(IAdminReportingRepository reportingRepository)
    : IRequestHandler<GetAdminSalesReportQuery, Result<AdminSalesReportDto>>
{
    public async Task<Result<AdminSalesReportDto>> Handle(
        GetAdminSalesReportQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = await reportingRepository.GetSalesReportAsync(
            request.FromUtc,
            request.ToUtc,
            request.TopProducts,
            cancellationToken);

        var dailySales = snapshot.DailySales
            .Select(day => new DailySalesDto(
                day.Date,
                day.SuccessfulPayments,
                day.RefundedPayments,
                day.GrossRevenue,
                day.RefundedAmount,
                day.GrossRevenue - day.RefundedAmount))
            .ToArray();
        var topProducts = snapshot.TopProducts
            .Select(product => new TopSellingProductDto(
                product.ProductId,
                product.ProductName,
                product.Quantity,
                product.GrossRevenue))
            .ToArray();

        return Result<AdminSalesReportDto>.Success(new AdminSalesReportDto(
            request.FromUtc,
            request.ToUtc,
            snapshot.OrdersCreated,
            snapshot.PaidOrders,
            snapshot.RefundedOrders,
            snapshot.SuccessfulPayments,
            snapshot.RefundedPayments,
            snapshot.GrossRevenue,
            snapshot.RefundedAmount,
            snapshot.GrossRevenue - snapshot.RefundedAmount,
            dailySales,
            topProducts,
            DateTimeOffset.UtcNow));
    }
}
