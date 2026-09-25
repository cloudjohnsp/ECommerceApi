using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Administration;
using ECommerce.Application.Administration.Handlers;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Administration.Handlers;

public sealed class GetAdminDashboardHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsRepositorySnapshotWithGenerationTimestamp()
    {
        var repository = new Mock<IAdminReportingRepository>();
        var snapshot = new AdminDashboardSnapshot(4, 8, 2, 3, 1, 2, 1, 749.50m);
        repository.Setup(item => item.GetDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var handler = new GetAdminDashboardHandler(repository.Object);
        var before = DateTimeOffset.UtcNow;

        var result = await handler.Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(snapshot, options => options.ExcludingMissingMembers());
        result.Value!.GeneratedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        repository.Verify(item => item.GetDashboardAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
