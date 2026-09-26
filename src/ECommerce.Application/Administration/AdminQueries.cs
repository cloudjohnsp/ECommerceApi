using ECommerce.Application.Administration.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Administration;

public sealed record GetAdminDashboardQuery : IRequest<Result<AdminDashboardDto>>;

public sealed record GetAdminSalesReportQuery(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int TopProducts = 10) : IRequest<Result<AdminSalesReportDto>>;
