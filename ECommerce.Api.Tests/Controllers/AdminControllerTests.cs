using System.Reflection;
using ECommerce.Api.Controllers;
using ECommerce.Api.Contracts.Administration;
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
        var dashboard = new AdminDashboardDto(
            1, 2, 3, 4, 5, 6, 7, 8, 9, 800m, 125m, DateTimeOffset.UtcNow);
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

    [Fact]
    public async Task GetDashboard_WhenFeatureIsDisabled_ReturnsNotFound()
    {
        var mediator = new Mock<ISender>();
        mediator.Setup(item => item.Send(It.IsAny<GetAdminDashboardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AdminDashboardDto>.Failure("Admin dashboard is disabled."));
        var controller = new AdminController(mediator.Object);

        var response = await controller.GetDashboard(CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSalesReport_DispatchesPeriodAndReturnsReport()
    {
        var fromUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var toUtc = fromUtc.AddDays(30);
        var report = new AdminSalesReportDto(
            fromUtc, toUtc, 4, 3, 1, 3, 1, 450m, 75m, 375m, [], [], DateTimeOffset.UtcNow);
        var mediator = new Mock<ISender>();
        mediator.Setup(item => item.Send(
                new GetAdminSalesReportQuery(fromUtc, toUtc, 7),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AdminSalesReportDto>.Success(report));
        var controller = new AdminController(mediator.Object);

        var response = await controller.GetSalesReport(
            new SalesReportRequest(fromUtc, toUtc, 7),
            CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(report);
        mediator.VerifyAll();
    }
}
