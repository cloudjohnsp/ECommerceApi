using ECommerce.Api.Contracts.Users;
using ECommerce.Application.Users.Commands;
using ECommerce.Application.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

public sealed class UserController(ISender mediator) : BaseApiController
{
    private readonly ISender _mediator = mediator;

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        CreateUserCommand command = new(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Password,
            request.Role
        );

        var createdUser = await _mediator.Send(command);

        if (createdUser.IsFailure)
        {
            return BadRequest(createdUser.Errors);
        }

        return Created("", createdUser.Value);
    }

    [HttpGet("{userId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
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

        return Ok(user.Value);
    }
}
