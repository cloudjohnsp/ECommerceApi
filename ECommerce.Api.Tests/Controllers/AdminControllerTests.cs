using System.Reflection;
using ECommerce.Api.Controllers;
using ECommerce.Application.Administration;
using ECommerce.Application.Administration.Dtos;
using ECommerce.Shared.Results;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ECommerce.Api.Tests.Controllers;

public sealed class AdminControllerTests
{
    [Fact]
    public async Task GetDashboard_DispatchesQueryAndReturnsDashboard()
    {
        var mediator = new Mock<ISender>();
        var dashboard = new AdminDashboardDto(1, 2, 3, 4, 5, 6, 7, 800m, DateTimeOffset.UtcNow);
        mediator.Setup(item => item.Send(It.IsAny<GetAdminDashboardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AdminDashboardDto>.Success(dashboard));
        var controller = new AdminController(mediator.Object);

        var response = await controller.GetDashboard(CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().Be(dashboard);
        mediator.Verify(item => item.Send(
            It.IsAny<GetAdminDashboardQuery>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Controller_RequiresAdministratorRole()
    {
        var attribute = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Roles.Should().Be("Administrator");
    }
}
