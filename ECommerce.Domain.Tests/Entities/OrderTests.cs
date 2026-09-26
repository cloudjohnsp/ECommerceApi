using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public sealed class OrderTests
{
    [Fact]
    public void Create_WithValidCustomer_ReturnsPendingOrder()
    {
        var customerId = Guid.NewGuid();
        var result = Order.Create(customerId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CustomerId.Should().Be(customerId);
        result.Value.Status.Should().Be(OrderStatus.Pending);
        result.Value.Items.Should().BeEmpty();
        result.Value.Total.Should().Be(0);
    }

    [Fact]
    public void Create_WithEmptyCustomer_ReturnsFailure() =>
        Order.Create(Guid.Empty).IsFailure.Should().BeTrue();

    [Fact]
    public void AddItem_WithValidData_AddsItemAndCalculatesTotal()
    {
        var order = OrderFactory.Create(withItem: false);
        var productId = Guid.NewGuid();

        var result = order.AddItem(productId, "Notebook", 250m, 2);

        result.IsSuccess.Should().BeTrue();
        order.Items.Should().ContainSingle();
        order.Items.Single().ProductId.Should().Be(productId);
        order.Total.Should().Be(500m);
    }

    [Fact]
    public void AddItem_WithDuplicateProduct_ReturnsFailure()
    {
        var order = OrderFactory.Create(withItem: false);
        var productId = Guid.NewGuid();
        order.AddItem(productId, "Notebook", 100, 1);

        var result = order.AddItem(productId, "Notebook", 100, 1);

        result.IsFailure.Should().BeTrue();
        order.Items.Should().ContainSingle();
    }

    [Fact]
    public void AddItem_WithProductNameLongerThanPersistenceLimit_ReturnsFailure()
    {
        var order = OrderFactory.Create(withItem: false);

        var result = order.AddItem(Guid.NewGuid(), new string('a', 151), 100m, 1);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(
            "Product name must contain between 1 and 150 characters.");
        order.Items.Should().BeEmpty();
    }

    [Fact]
    public void AddItem_WhenSubtotalExceedsPersistablePaymentAmount_ReturnsFailure()
    {
        var order = OrderFactory.Create(withItem: false);

        var result = order.AddItem(Guid.NewGuid(), "Notebook", Order.MaximumTotal, 2);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain($"Order total cannot exceed {Order.MaximumTotal}.");
        order.Items.Should().BeEmpty();
    }

    [Fact]
    public void AddItem_WhenCumulativeTotalExceedsPersistablePaymentAmount_ReturnsFailure()
    {
        var order = OrderFactory.Create(withItem: false);
        order.AddItem(Guid.NewGuid(), "First", 1m, 1).IsSuccess.Should().BeTrue();

        var result = order.AddItem(Guid.NewGuid(), "Second", Order.MaximumTotal, 1);

        result.IsFailure.Should().BeTrue();
        order.Items.Should().ContainSingle();
        order.Total.Should().Be(1m);
    }

    [Fact]
    public void AddItem_WithUnsupportedUnitPriceScale_ReturnsFailure()
    {
        var order = OrderFactory.Create(withItem: false);

        var result = order.AddItem(Guid.NewGuid(), "Notebook", 10.999m, 1);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Unit price cannot have more than 2 decimal places.");
    }

    [Fact]
    public void MarkAsPaid_WithItems_ChangesStatus()
    {
        var order = OrderFactory.Create();

        var result = order.MarkAsPaid();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public void MarkAsPaid_WhenEmpty_ReturnsFailure()
    {
        var order = OrderFactory.Create(withItem: false);

        order.MarkAsPaid().IsFailure.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public void Cancel_WhenPending_CancelsOrder()
    {
        var order = OrderFactory.Create();

        var result = order.Cancel();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_WhenPaid_ReturnsFailure()
    {
        var order = OrderFactory.Create();
        order.MarkAsPaid();

        order.Cancel().IsFailure.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public void MarkAsRefunded_WhenPaid_ChangesStatusIdempotently()
    {
        var order = OrderFactory.Create();
        order.MarkAsPaid();

        var first = order.MarkAsRefunded();
        var second = order.MarkAsRefunded();

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Refunded);
        order.RefundedAt.Should().NotBeNull();
        order.Cancel().IsFailure.Should().BeTrue();
        order.MarkAsPaid().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarkAsRefunded_WhenPending_ReturnsFailure()
    {
        var order = OrderFactory.Create();

        order.MarkAsRefunded().IsFailure.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Pending);
    }
}
