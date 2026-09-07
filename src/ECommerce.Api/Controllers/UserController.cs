using ECommerce.Api.Contracts.Users;
using ECommerce.Application.Users;
using ECommerce.Application.Users.Commands;
using ECommerce.Application.Users.Dtos;
using ECommerce.Application.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

public sealed class UserController(ISender mediator) : BaseApiController
{
    private readonly ISender _mediator = mediator;

    [HttpGet("{userId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById([FromRoute] Guid userId)
    {
        GetUserByIdQuery query = new(userId);
        var user = await _mediator.Send(query);
        if (user.IsFailure)
        {
            return NotFound(user.Errors);
        }

        return Ok(ToResponse(user.Value!));
    }

    [HttpPut("{userId:guid}/profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(Guid userId, [FromBody] UpdateUserProfileRequest request)
    {
        var result = await _mediator.Send(new UpdateUserProfileCommand(userId, request.FirstName, request.LastName, request.Email));
        if (result.IsFailure)
        {
            return result.Errors.Contains("User not found.") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }

        return Ok(ToResponse(result.Value!));
    }

    [HttpPut("{userId:guid}/password")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(Guid userId, [FromBody] ChangePasswordRequest request)
    {
        var result = await _mediator.Send(new ChangeUserPasswordCommand(userId, request.Password));
        if (result.IsFailure)
        {
            return result.Errors.Contains("User not found.") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }

        return Ok(ToResponse(result.Value!));
    }

    [HttpPut("{userId:guid}/role")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeRole(Guid userId, [FromBody] ChangeRoleRequest request)
    {
        var result = await _mediator.Send(new ChangeUserRoleCommand(userId, request.Role));
        if (result.IsFailure)
        {
            return result.Errors.Contains("User not found.") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }

        return Ok(ToResponse(result.Value!));
    }

    [HttpDelete("{userId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid userId)
    {
        var result = await _mediator.Send(new DeleteUserCommand(userId));
        if (result.IsFailure)
        {
            return result.Errors.Contains("User not found.") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }

        return NoContent();
    }

    private static UserResponse ToResponse(UserDto user)
    {
        return new UserResponse(user.Id.ToString(), user.FirstName, user.LastName, user.Email, user.Role);
    }
}
