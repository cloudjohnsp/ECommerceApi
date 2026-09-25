using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class AdminReportingRepositoryTests
{
    [Fact]
    public async Task GetDashboardAsync_AggregatesCurrentBusinessState()
    {
        await using var context = CreateContext();
        var activeUser = User.Create(
            "Active", "Customer", Email.Create("active@example.com").Value!, "hash").Value!;
        var inactiveUser = User.Create(
            "Inactive", "Customer", Email.Create("inactive@example.com").Value!, "hash").Value!;
        inactiveUser.Deactivate();
        var activeProduct = Product.Create("Active product", "", 100m, 10).Value!;
        var inactiveProduct = Product.Create("Inactive product", "", 50m, 5).Value!;
        inactiveProduct.Deactivate();
        var pendingOrder = CreateOrder(activeUser.Id, activeProduct);
        var paidOrder = CreateOrder(activeUser.Id, activeProduct);
        paidOrder.MarkAsPaid();
        var cancelledOrder = CreateOrder(activeUser.Id, activeProduct);
        cancelledOrder.Cancel();
        var refundedOrder = CreateOrder(activeUser.Id, activeProduct);
        refundedOrder.MarkAsPaid();
        refundedOrder.MarkAsRefunded();
        var pendingPayment = Payment.Create(pendingOrder.Id, pendingOrder.Total, "test").Value!;
        var paidPayment = Payment.Create(paidOrder.Id, paidOrder.Total, "test").Value!;
        paidPayment.MarkAsPaid("pay_paid");
        var failedPayment = Payment.Create(cancelledOrder.Id, cancelledOrder.Total, "test").Value!;
        failedPayment.MarkAsFailed();
        var refundedPayment = Payment.Create(refundedOrder.Id, refundedOrder.Total, "test").Value!;
        refundedPayment.MarkAsPaid("pay_refunded");
        refundedPayment.MarkAsRefunded();

        await context.AddRangeAsync(
            activeUser, inactiveUser, activeProduct, inactiveProduct,
            pendingOrder, paidOrder, cancelledOrder, refundedOrder,
            pendingPayment, paidPayment, failedPayment, refundedPayment);
        await context.SaveChangesAsync();
        var repository = new AdminReportingRepository(context);

        var result = await repository.GetDashboardAsync();

        result.Should().Be(new AdminDashboardSnapshot(
            ActiveUsers: 1,
            ActiveProducts: 1,
            PendingOrders: 1,
            PaidOrders: 1,
            CancelledOrders: 1,
            RefundedOrders: 1,
            PendingPayments: 1,
            FailedPayments: 1,
            RefundedPayments: 1,
            PaidRevenue: 100m,
            RefundedAmount: 100m));
    }

    private static Order CreateOrder(Guid customerId, Product product)
    {
        var order = Order.Create(customerId).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 1);
        return order;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
