using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Administration;
using ECommerce.Application.Administration.Handlers;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Administration.Handlers;

public sealed class GetAdminSalesReportHandlerTests
{
    [Fact]
    public async Task Handle_MapsReportAndCalculatesNetRevenue()
    {
        var fromUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var toUtc = fromUtc.AddDays(2);
        var productId = Guid.NewGuid();
        var snapshot = new AdminSalesReportSnapshot(
            OrdersCreated: 4,
            PaidOrders: 3,
            RefundedOrders: 1,
            SuccessfulPayments: 3,
            RefundedPayments: 1,
            GrossRevenue: 450m,
            RefundedAmount: 75m,
            DailySales:
            [
                new DailySalesSnapshot(new DateOnly(2026, 1, 1), 3, 1, 450m, 75m)
            ],
            TopProducts:
            [
                new TopSellingProductSnapshot(productId, "Mechanical keyboard", 5, 350m)
            ]);
        var repository = new Mock<IAdminReportingRepository>();
        repository.Setup(item => item.GetSalesReportAsync(
                fromUtc, toUtc, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        var handler = new GetAdminSalesReportHandler(repository.Object);
        var before = DateTimeOffset.UtcNow;

        var result = await handler.Handle(
            new GetAdminSalesReportQuery(fromUtc, toUtc, 5),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NetRevenue.Should().Be(375m);
        result.Value.DailySales.Should().ContainSingle().Which.NetRevenue.Should().Be(375m);
        result.Value.TopProducts.Should().ContainSingle().Which.ProductId.Should().Be(productId);
        result.Value.GeneratedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        repository.VerifyAll();
    }
}
