using System.Security.Claims;
using ECommerce.Api.Contracts.Payments;
using ECommerce.Api.Controllers;
using ECommerce.Application.Payments;
using ECommerce.Application.Payments.Dtos;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ECommerce.Api.Tests.Controllers;

public sealed class PaymentsControllerTests
{
    [Fact]
    public async Task Create_WhenExistingPaymentUsesAnotherCurrency_ReturnsConflict()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var errors = new[] { "Payment currency does not match the existing payment." };
        var mediator = new Mock<ISender>();
        mediator.Setup(sender => sender.Send(
                It.Is<CreatePaymentCommand>(command =>
                    command.OrderId == orderId &&
                    command.Currency == "BRL" &&
                    command.CustomerId == customerId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentDto>.Failure(errors));
        var controller = new PaymentsController(mediator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, customerId.ToString()),
                        new Claim(ClaimTypes.Role, UserRole.Customer.ToString())
                    ], "Test"))
                }
            }
        };

        var response = await controller.Create(
            new CreatePaymentRequest(orderId, "BRL"),
            CancellationToken.None);

        response.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().BeEquivalentTo(errors);
    }
}
