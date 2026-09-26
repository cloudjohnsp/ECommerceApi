using System.Text;
using ECommerce.Application.Payments;
using MediatR;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Controllers;

[AllowAnonymous]
[ApiController]
[ApiVersion(1.0)]
[Route("api/webhooks/payments")]
[Route("api/v{version:apiVersion}/webhooks/payments")]
public sealed class PaymentWebhooksController(ISender mediator) : ControllerBase
{
    private const int MaximumPayloadSizeBytes = 64 * 1024;

    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(MaximumPayloadSizeBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        var payload = await ReadPayloadAsync(Request, cancellationToken);
        if (payload is null)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        var signature = Request.Headers["X-Payment-Signature"].FirstOrDefault();

        var result = await mediator.Send(
            new ProcessPaymentWebhookCommand(payload, signature),
            cancellationToken);

        if (result.IsSuccess) return NoContent();
        if (result.Errors.Contains("Invalid payment webhook signature.")) return Unauthorized(result.Errors);
        if (result.Errors.Contains("Payment not found.")) return NotFound(result.Errors);
        return BadRequest(result.Errors);
    }

    private static async Task<string?> ReadPayloadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumPayloadSizeBytes)
            return null;

        using var payload = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var remaining = MaximumPayloadSizeBytes + 1 - (int)payload.Length;
            if (remaining <= 0)
                return null;

            var bytesRead = await request.Body.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                cancellationToken);
            if (bytesRead == 0)
                return Encoding.UTF8.GetString(payload.GetBuffer(), 0, (int)payload.Length);

            await payload.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            if (payload.Length > MaximumPayloadSizeBytes)
                return null;
        }
    }
}
