using System.Security.Claims;
using ECommerce.Api.Contracts.Auth;
using ECommerce.Api.Contracts.Users;
using ECommerce.Api.Controllers;
using ECommerce.Application.Users;
using ECommerce.Application.Users.Dtos;
using ECommerce.Api.Contracts.Orders;
using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Dtos;
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

public sealed class AuthorizationControllerTests
{
    [Fact]
    public async Task Register_AlwaysCreatesCustomerAccount()
    {
        var mediator = new Mock<ISender>();
        var created = new UserDto(
            Guid.NewGuid(), "Jane", "Doe", "jane@example.com", UserRole.Customer, true);
        mediator.Setup(x => x.Send(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserDto>.Success(created));
        var controller = new AuthController(mediator.Object);

        var result = await controller.Register(
            new RegisterRequest("Jane", "Doe", "jane@example.com", "Password123!"));

        result.Should().BeOfType<CreatedResult>();
        mediator.Verify(x => x.Send(
            It.Is<CreateUserCommand>(command => command.Role == UserRole.Customer),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_ForAnotherCustomer_ReturnsForbiddenWithoutDispatchingCommand()
    {
        var mediator = new Mock<ISender>(MockBehavior.Strict);
        var controller = new UserController(mediator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(Guid.NewGuid(), UserRole.Customer)
                }
            }
        };

        var result = await controller.UpdateProfile(
            Guid.NewGuid(),
            new UpdateUserProfileRequest("Other", null, null));

        result.Should().BeOfType<ForbidResult>();
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ChangePassword_AdministratorChangingAnotherUser_ReturnsForbidden()
    {
        var mediator = new Mock<ISender>(MockBehavior.Strict);
        var controller = new UserController(mediator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(Guid.NewGuid(), UserRole.Administrator)
                }
            }
        };

        var result = await controller.ChangePassword(
            Guid.NewGuid(),
            new ChangePasswordRequest("Password123!"));

        result.Should().BeOfType<ForbidResult>();
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateOrder_CustomerCannotSubmitOrderForAnotherCustomer()
    {
        var authenticatedUserId = Guid.NewGuid();
        var mediator = new Mock<ISender>();
        mediator.Setup(x => x.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderDto>.Failure("Expected test response."));
        var controller = new OrdersController(mediator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(authenticatedUserId, UserRole.Customer)
                }
            }
        };

        await controller.Create(
            new CreateOrderRequest(
                Guid.NewGuid(),
                [new CreateOrderItemRequest(Guid.NewGuid(), 1)]),
            CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<CreateOrderCommand>(command => command.CustomerId == authenticatedUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundPayment_CustomerScopeComesFromAuthenticatedUser()
    {
        var authenticatedUserId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var mediator = new Mock<ISender>();
        mediator.Setup(x => x.Send(It.IsAny<RefundPaymentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentDto>.Failure("Expected test response."));
        var controller = new PaymentsController(mediator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(authenticatedUserId, UserRole.Customer)
                }
            }
        };

        await controller.Refund(
            orderId,
            new ECommerce.Api.Contracts.Payments.RefundPaymentRequest("customer_request"),
            CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<RefundPaymentCommand>(command =>
                command.OrderId == orderId && command.CustomerId == authenticatedUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, UserRole role) => new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        ],
        "Test"));
}
