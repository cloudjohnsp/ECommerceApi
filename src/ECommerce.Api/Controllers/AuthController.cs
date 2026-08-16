using ECommerce.Api.Contracts.Auth;
using ECommerce.Application.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[AllowAnonymous]
public sealed class AuthController(ISender mediator) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await mediator.Send(command);

        if (result.IsFailure)
        {
            return Unauthorized(result.Errors);
        }

        return Ok(new { accessToken = result.Value });
    }
}
