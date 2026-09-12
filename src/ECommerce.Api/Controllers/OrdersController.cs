using ECommerce.Api.Contracts.Orders;
using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

[Authorize]
public sealed class OrdersController(ISender mediator) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOrdersQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOrderByIdQuery(orderId), cancellationToken);
        return result.IsFailure ? NotFound(result.Errors) : Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateOrderCommand(request.CustomerId,
            [.. request.Items.Select(item => new CreateOrderItem(item.ProductId, item.Quantity))]);
        var result = await mediator.Send(command, cancellationToken);
        if (result.IsFailure) return BadRequest(result.Errors);
        return CreatedAtAction(nameof(GetById), new { orderId = result.Value!.Id }, result.Value);
    }

    [HttpPut("{orderId:guid}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid orderId, UpdateOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateOrderCommand(orderId, request.Status), cancellationToken);
        return result.IsFailure
            ? result.Errors.Contains("Order not found.") ? NotFound(result.Errors) : BadRequest(result.Errors)
            : Ok(result.Value);
    }

    [HttpDelete("{orderId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteOrderCommand(orderId), cancellationToken);
        return result.IsFailure
            ? result.Errors.Contains("Order not found.") ? NotFound(result.Errors) : BadRequest(result.Errors)
            : NoContent();
    }
}
