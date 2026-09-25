using ECommerce.Application.Abstractions.Features;
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
        var snapshot = new AdminDashboardSnapshot(4, 8, 2, 3, 1, 1, 2, 1, 1, 749.50m, 99.90m);
        repository.Setup(item => item.GetDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var featureFlags = new Mock<IFeatureFlagService>();
        featureFlags.Setup(item => item.IsEnabled(FeatureFlagNames.AdminDashboard)).Returns(true);
        var handler = new GetAdminDashboardHandler(repository.Object, featureFlags.Object);
        var before = DateTimeOffset.UtcNow;

        var result = await handler.Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(snapshot, options => options.ExcludingMissingMembers());
        result.Value!.GeneratedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        repository.Verify(item => item.GetDashboardAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFeatureIsDisabled_ReturnsFailureWithoutQueryingDatabase()
    {
        var repository = new Mock<IAdminReportingRepository>(MockBehavior.Strict);
        var featureFlags = new Mock<IFeatureFlagService>();
        featureFlags.Setup(item => item.IsEnabled(FeatureFlagNames.AdminDashboard)).Returns(false);
        var handler = new GetAdminDashboardHandler(repository.Object, featureFlags.Object);

        var result = await handler.Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle("Admin dashboard is disabled.");
        repository.VerifyNoOtherCalls();
    }
}
