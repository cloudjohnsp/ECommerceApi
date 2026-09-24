using ECommerce.Api.Contracts.Auth;
using ECommerce.Api.Contracts.Users;
using ECommerce.Application.Auth;
using ECommerce.Application.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ECommerce.Api.Security;

namespace ECommerce.Api.Controllers;

[AllowAnonymous]
[EnableRateLimiting(ApiRateLimitPolicy.Authentication)]
public sealed class AuthController(ISender mediator) : BaseApiController
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await mediator.Send(command);

        if (result.IsFailure)
        {
            return Unauthorized(result.Errors);
        }
        var response = new AuthResponse(
            result.Value!.AccessToken,
            result.Value.RefreshToken,
            result.Value.ExpiresIn
        );

        return Ok(response);
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        CreateUserCommand command = new(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            ECommerce.Domain.Enums.UserRole.Customer
        );

        var createdUser = await mediator.Send(command);

        if (createdUser.IsFailure)
        {
            return BadRequest(createdUser.Errors);
        }

        var response = new RegisterResponse(
            createdUser.Value!.Id,
            createdUser.Value.FirstName,
            createdUser.Value.LastName,
            createdUser.Value.Email,
            createdUser.Value.Role
        );

        return Created("", response);
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
        var response = new AuthResponse(
            result.Value!.AccessToken,
            result.Value.RefreshToken,
            result.Value.ExpiresIn
        );
        return Ok(response);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await mediator.Send(new LogoutCommand(request.RefreshToken));
        return NoContent();
    }
}
