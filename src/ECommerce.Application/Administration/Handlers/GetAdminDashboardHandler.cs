using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Administration.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Administration.Handlers;

public sealed class GetAdminDashboardHandler(IAdminReportingRepository reportingRepository)
    : IRequestHandler<GetAdminDashboardQuery, Result<AdminDashboardDto>>
{
    public async Task<Result<AdminDashboardDto>> Handle(
        GetAdminDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var snapshot = await reportingRepository.GetDashboardAsync(cancellationToken);
        var dashboard = new AdminDashboardDto(
            snapshot.ActiveUsers,
            snapshot.ActiveProducts,
            snapshot.PendingOrders,
            snapshot.PaidOrders,
            snapshot.CancelledOrders,
            snapshot.PendingPayments,
            snapshot.FailedPayments,
            snapshot.PaidRevenue,
            DateTimeOffset.UtcNow);

        return Result<AdminDashboardDto>.Success(dashboard);
    }
}
