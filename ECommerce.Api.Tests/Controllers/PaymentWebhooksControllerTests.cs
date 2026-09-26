using System.Text;
using ECommerce.Api.Controllers;
using ECommerce.Application.Payments;
using ECommerce.Shared.Results;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ECommerce.Api.Tests.Controllers;

public sealed class PaymentWebhooksControllerTests
{
    [Fact]
    public async Task Handle_WithBoundedPayload_DispatchesExactBodyAndSignature()
    {
        const string payload = "{\"event\":\"payment.approved\"}";
        const string signature = "signature";
        var mediator = new Mock<ISender>();
        mediator.Setup(sender => sender.Send(
                It.Is<ProcessPaymentWebhookCommand>(command =>
                    command.Payload == payload && command.Signature == signature),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.Headers["X-Payment-Signature"] = signature;
        var controller = new PaymentWebhooksController(mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = await controller.Handle(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        mediator.VerifyAll();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_WhenPayloadExceedsLimit_ReturnsPayloadTooLargeWithoutDispatching(
        bool includeContentLength)
    {
        var mediator = new Mock<ISender>(MockBehavior.Strict);
        mediator.Setup(sender => sender.Send(
                It.IsAny<ProcessPaymentWebhookCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var payload = Encoding.UTF8.GetBytes(new string('x', 65 * 1024));
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(payload);
        context.Request.ContentLength = includeContentLength ? payload.Length : null;
        var controller = new PaymentWebhooksController(mediator.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = await controller.Handle(CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        mediator.Verify(sender => sender.Send(
            It.IsAny<ProcessPaymentWebhookCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
