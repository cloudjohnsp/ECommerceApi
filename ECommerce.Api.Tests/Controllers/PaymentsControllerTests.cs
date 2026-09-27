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
    public async Task Get_ReturnsCompletePaymentAttemptHistory()
    {
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        IReadOnlyCollection<PaymentDto> attempts =
        [
            new PaymentDto(
                Guid.NewGuid(), orderId, "attempt-2", 10m, "BRL",
                "ECommercePayment", PaymentStatus.Pending, null,
                DateTimeOffset.UtcNow, null, null, null),
            new PaymentDto(
                Guid.NewGuid(), orderId, "attempt-1", 10m, "BRL",
                "ECommercePayment", PaymentStatus.Failed, "pay_1",
                DateTimeOffset.UtcNow.AddMinutes(-1), null,
                DateTimeOffset.UtcNow, null)
        ];
        var mediator = new Mock<ISender>();
        mediator.Setup(sender => sender.Send(
                It.Is<GetPaymentsByOrderIdQuery>(query =>
                    query.OrderId == orderId && query.CustomerId == customerId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<PaymentDto>>.Success(attempts));
        var controller = CreateController(mediator, customerId);

        var response = await controller.Get(orderId, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(attempts);
    }

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
                    command.IdempotencyKey == "attempt-1" &&
                    command.CustomerId == customerId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentDto>.Failure(errors));
        var controller = CreateController(mediator, customerId);

        var response = await controller.Create(
            new CreatePaymentRequest(orderId, "BRL", "attempt-1"),
            CancellationToken.None);

        response.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().BeEquivalentTo(errors);
    }

    private static PaymentsController CreateController(
        Mock<ISender> mediator,
        Guid customerId) => new(mediator.Object)
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
}
