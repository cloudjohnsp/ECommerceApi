using ECommerce.Application.Abstractions.Features;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Administration.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Administration.Handlers;

public sealed class GetAdminDashboardHandler(
    IAdminReportingRepository reportingRepository,
    IFeatureFlagService featureFlags)
    : IRequestHandler<GetAdminDashboardQuery, Result<AdminDashboardDto>>
{
    public async Task<Result<AdminDashboardDto>> Handle(
        GetAdminDashboardQuery request,
        CancellationToken cancellationToken)
    {
        if (!featureFlags.IsEnabled(FeatureFlagNames.AdminDashboard))
            return Result<AdminDashboardDto>.Failure("Admin dashboard is disabled.");

        var snapshot = await reportingRepository.GetDashboardAsync(cancellationToken);
        var dashboard = new AdminDashboardDto(
            snapshot.ActiveUsers,
            snapshot.ActiveProducts,
            snapshot.PendingOrders,
            snapshot.PaidOrders,
            snapshot.CancelledOrders,
            snapshot.RefundedOrders,
            snapshot.PendingPayments,
            snapshot.FailedPayments,
            snapshot.RefundedPayments,
            snapshot.PaidRevenue,
            snapshot.RefundedAmount,
            DateTimeOffset.UtcNow);

        return Result<AdminDashboardDto>.Success(dashboard);
    }
}
