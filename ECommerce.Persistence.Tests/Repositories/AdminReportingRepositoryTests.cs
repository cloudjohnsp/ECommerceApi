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

    [Fact]
    public async Task GetSalesReportAsync_AggregatesPeriodDailySalesAndTopProducts()
    {
        await using var context = CreateContext();
        var customer = User.Create(
            "Customer", "One", Email.Create("sales@example.com").Value!, "hash").Value!;
        var keyboard = Product.Create("Keyboard", "", 100m, 10).Value!;
        var mouse = Product.Create("Mouse", "", 50m, 10).Value!;
        var paidOrder = Order.Create(customer.Id).Value!;
        paidOrder.AddItem(keyboard.Id, keyboard.Name, keyboard.Price, 2);
        paidOrder.AddItem(mouse.Id, mouse.Name, mouse.Price, 1);
        paidOrder.MarkAsPaid();
        var refundedOrder = Order.Create(customer.Id).Value!;
        refundedOrder.AddItem(mouse.Id, mouse.Name, mouse.Price, 1);
        refundedOrder.MarkAsPaid();
        refundedOrder.MarkAsRefunded();
        var paidPayment = Payment.Create(paidOrder.Id, paidOrder.Total, "test").Value!;
        paidPayment.MarkAsPaid("pay_report_paid");
        var refundedPayment = Payment.Create(refundedOrder.Id, refundedOrder.Total, "test").Value!;
        refundedPayment.MarkAsPaid("pay_report_refunded");
        refundedPayment.MarkAsRefunded();
        await context.AddRangeAsync(
            customer, keyboard, mouse, paidOrder, refundedOrder, paidPayment, refundedPayment);
        await context.SaveChangesAsync();
        var repository = new AdminReportingRepository(context);
        var fromUtc = DateTimeOffset.UtcNow.AddHours(-1);
        var toUtc = DateTimeOffset.UtcNow.AddHours(1);

        var result = await repository.GetSalesReportAsync(fromUtc, toUtc, 1);

        result.OrdersCreated.Should().Be(2);
        result.PaidOrders.Should().Be(2);
        result.RefundedOrders.Should().Be(1);
        result.SuccessfulPayments.Should().Be(2);
        result.RefundedPayments.Should().Be(1);
        result.GrossRevenue.Should().Be(300m);
        result.RefundedAmount.Should().Be(50m);
        result.DailySales.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new DailySalesSnapshot(DateOnly.FromDateTime(DateTime.UtcNow), 2, 1, 300m, 50m));
        result.TopProducts.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new TopSellingProductSnapshot(keyboard.Id, "Keyboard", 2, 200m));
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
