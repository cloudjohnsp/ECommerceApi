using ECommerce.Application.Administration;
using ECommerce.Application.Administration.Dtos;
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
}
