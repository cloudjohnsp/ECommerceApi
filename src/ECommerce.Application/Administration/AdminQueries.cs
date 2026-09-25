using ECommerce.Application.Administration.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Administration;

public sealed record GetAdminDashboardQuery : IRequest<Result<AdminDashboardDto>>;
