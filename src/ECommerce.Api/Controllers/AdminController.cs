using ECommerce.Application.Administration;
using ECommerce.Application.Administration.Dtos;
using ECommerce.Api.Contracts.Administration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class AdminController(ISender mediator) : BaseApiController
{
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AdminDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAdminDashboardQuery(), cancellationToken);
        return result.IsFailure ? NotFound(result.Errors) : Ok(result.Value);
    }

    [HttpGet("reports/sales")]
    [ProducesResponseType(typeof(AdminSalesReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSalesReport(
        [FromQuery] SalesReportRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetAdminSalesReportQuery(request.FromUtc, request.ToUtc, request.TopProducts);
        var result = await mediator.Send(query, cancellationToken);
        return result.IsFailure ? BadRequest(result.Errors) : Ok(result.Value);
    }
}
