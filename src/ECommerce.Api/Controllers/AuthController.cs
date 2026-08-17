using ECommerce.Api.Contracts.Auth;
using ECommerce.Application.Auth.Commands;
using ECommerce.Application.Auth.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

[AllowAnonymous]
public sealed class AuthController(ISender mediator) : BaseApiController
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await mediator.Send(command);

        if (result.IsFailure)
        {
            return Unauthorized(result.Errors);
        }

        return Ok(ToResponse(result.Value!));
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await mediator.Send(command);

        if (result.IsFailure)
        {
            return Unauthorized(result.Errors);
        }

        return Ok(ToResponse(result.Value!));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await mediator.Send(new LogoutCommand(request.RefreshToken));
        return NoContent();
    }

    public static AuthResponse ToResponse(AuthTokensDto tokens) =>
        new(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn);
}
