using ECommerce.Api.Contracts.Users;
using ECommerce.Application.Users;
using ECommerce.Application.Users.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerce.Api.Authorization;
using ECommerce.Shared.Pagination;

namespace ECommerce.Api.Controllers;

[Authorize]
public sealed class UserController(ISender mediator) : BaseApiController
{
    private readonly ISender _mediator = mediator;

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!User.CanAccessUser(userId)) return Forbid();

        GetUserByIdQuery query = new(userId);
        var user = await _mediator.Send(query, cancellationToken);
        if (user.IsFailure)
        {
            return NotFound(user.Errors);
        }

        return Ok(ToResponse(user.Value!));
    }

    [HttpGet("{userId:guid}/history")]
    [ProducesResponseType(typeof(PagedResult<UserAuditEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        Guid userId,
        [FromQuery] UserAuditSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.CanAccessUser(userId)) return Forbid();

        var result = await _mediator.Send(
            new GetUserAuditHistoryQuery(userId, request.Page, request.PageSize),
            cancellationToken);
        return result.IsFailure ? NotFound(result.Errors) : Ok(result.Value);
    }

    [HttpPut("{userId:guid}/profile")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        Guid userId,
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!User.CanAccessUser(userId)) return Forbid();
        if (!User.TryGetUserId(out var actorUserId)) return Unauthorized();

        var result = await _mediator.Send(new UpdateUserProfileCommand(
            userId,
            request.FirstName,
            request.LastName,
            request.Email,
            actorUserId), cancellationToken);
        if (result.IsFailure)
        {
            return result.Errors.Contains("User not found.") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }

        return Ok(ToResponse(result.Value!));
    }

    [HttpPut("{userId:guid}/password")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        Guid userId,
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!User.IsUser(userId)) return Forbid();
        if (!User.TryGetUserId(out var actorUserId)) return Unauthorized();

        var result = await _mediator.Send(new ChangeUserPasswordCommand(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            actorUserId), cancellationToken);
        if (result.IsFailure)
        {
            return result.Errors.Contains("User not found.") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }

        return Ok(ToResponse(result.Value!));
    }

    [HttpPut("{userId:guid}/role")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeRole(
        Guid userId,
        [FromBody] ChangeRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetUserId(out var actorUserId)) return Unauthorized();

        var result = await _mediator.Send(new ChangeUserRoleCommand(
            userId,
            request.Role,
            actorUserId), cancellationToken);
        if (result.IsFailure)
        {
            return result.Errors.Contains("User not found.") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }

        return Ok(ToResponse(result.Value!));
    }

    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!User.CanAccessUser(userId)) return Forbid();
        if (!User.TryGetUserId(out var actorUserId)) return Unauthorized();

        var result = await _mediator.Send(
            new DeleteUserCommand(userId, actorUserId),
            cancellationToken);
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
