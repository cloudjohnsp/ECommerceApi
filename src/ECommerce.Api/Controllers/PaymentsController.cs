using ECommerce.Api.Contracts.Payments;
using ECommerce.Application.Payments;
using ECommerce.Application.Payments.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerce.Api.Authorization;

namespace ECommerce.Api.Controllers;

[Authorize]
public sealed class PaymentsController(ISender mediator) : BaseApiController
{
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid orderId, CancellationToken cancellationToken)
    {
        if (!User.TryGetCustomerScope(out var customerId)) return Forbid();
        var result = await mediator.Send(new GetPaymentByOrderIdQuery(orderId, customerId), cancellationToken);
        return result.IsFailure ? NotFound(result.Errors) : Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Create(
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetCustomerScope(out var customerId)) return Forbid();
        var result = await mediator.Send(
            new CreatePaymentCommand(request.OrderId, request.Currency, customerId),
            cancellationToken);
        if (result.IsSuccess) return Ok(result.Value);

        if (result.Errors.Contains("Order not found.")) return NotFound(result.Errors);
        if (result.Errors.Any(error => error.StartsWith("Only pending orders", StringComparison.Ordinal)))
            return Conflict(result.Errors);
        if (result.Errors.Any(error => error.StartsWith("Payment gateway", StringComparison.Ordinal)))
            return StatusCode(StatusCodes.Status502BadGateway, result.Errors);

        return BadRequest(result.Errors);
    }

    [HttpPost("{orderId:guid}/refund")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Refund(
        Guid orderId,
        RefundPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetCustomerScope(out var customerId)) return Forbid();
        var result = await mediator.Send(
            new RefundPaymentCommand(orderId, request.Reason, customerId),
            cancellationToken);
        if (result.IsSuccess) return Ok(result.Value);

        if (result.Errors.Contains("Payment not found.")) return NotFound(result.Errors);
        if (result.Errors.Any(error => error.StartsWith("Only a paid", StringComparison.Ordinal)))
            return Conflict(result.Errors);
        if (result.Errors.Any(error => error.StartsWith("Payment gateway", StringComparison.Ordinal)))
            return StatusCode(StatusCodes.Status502BadGateway, result.Errors);

        return BadRequest(result.Errors);
    }
}
