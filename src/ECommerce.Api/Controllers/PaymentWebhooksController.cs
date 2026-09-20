using ECommerce.Application.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/webhooks/payments")]
public sealed class PaymentWebhooksController(ISender mediator) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["X-Payment-Signature"].FirstOrDefault();

        var result = await mediator.Send(
            new ProcessPaymentWebhookCommand(payload, signature),
            cancellationToken);

        if (result.IsSuccess) return NoContent();
        if (result.Errors.Contains("Invalid payment webhook signature.")) return Unauthorized(result.Errors);
        if (result.Errors.Contains("Payment not found.")) return NotFound(result.Errors);
        return BadRequest(result.Errors);
    }
}
