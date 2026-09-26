using ECommerce.Api.Contracts.Auth;
using ECommerce.Api.Controllers;
using ECommerce.Application.Auth;
using ECommerce.Shared.Results;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ECommerce.Api.Tests.Controllers;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task Logout_WhenCommandFails_ReturnsBadRequestWithErrors()
    {
        var errors = new[] { "Refresh token is required." };
        var mediator = new Mock<ISender>();
        mediator.Setup(sender => sender.Send(
                It.IsAny<LogoutCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(errors));
        var controller = new AuthController(mediator.Object);

        var response = await controller.Logout(new LogoutRequest(string.Empty));

        response.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public async Task Logout_WhenCommandSucceeds_ReturnsNoContent()
    {
        var mediator = new Mock<ISender>();
        mediator.Setup(sender => sender.Send(
                It.IsAny<LogoutCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var controller = new AuthController(mediator.Object);

        var response = await controller.Logout(new LogoutRequest("refresh-token"));

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Logout_PropagatesRequestCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var mediator = new Mock<ISender>();
        mediator.Setup(sender => sender.Send(
                It.IsAny<LogoutCommand>(),
                cancellation.Token))
            .ReturnsAsync(Result.Success());
        var controller = new AuthController(mediator.Object);

        await controller.Logout(
            new LogoutRequest("refresh-token"),
            cancellation.Token);

        mediator.Verify(sender => sender.Send(
            It.IsAny<LogoutCommand>(),
            cancellation.Token), Times.Once);
    }
}
