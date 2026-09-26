using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Specifications;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;

namespace ECommerce.Application.Tests.Orders.Specifications;

public sealed class OrderSearchSpecificationTests
{
    [Fact]
    public void Criteria_RestrictsCustomerAndStatus()
    {
        var customerId = Guid.NewGuid();
        var expected = OrderFactory.Create(customerId);
        expected.MarkAsPaid();
        var anotherStatus = OrderFactory.Create(customerId);
        var anotherCustomer = OrderFactory.Create(Guid.NewGuid());
        anotherCustomer.MarkAsPaid();
        var specification = new OrderSearchSpecification(
            new SearchOrdersQuery(customerId, OrderStatus.Paid));

        var result = new[] { expected, anotherStatus, anotherCustomer }
            .AsQueryable()
            .Where(specification.Criteria)
            .ToArray();

        result.Should().ContainSingle().Which.Id.Should().Be(expected.Id);
    }

    [Fact]
    public void Ordering_ByTotalDescendingAndPagination_AreApplied()
    {
        var lowerTotal = OrderFactory.Create(withItem: false);
        lowerTotal.AddItem(Guid.NewGuid(), "Mouse", 25m, 1);
        var higherTotal = OrderFactory.Create(withItem: false);
        higherTotal.AddItem(Guid.NewGuid(), "Keyboard", 100m, 2);
        var specification = new OrderSearchSpecification(
            new SearchOrdersQuery(SortBy: "total", Descending: true, Page: 2, PageSize: 5));

        var result = specification.ApplyOrdering(new[] { lowerTotal, higherTotal }.AsQueryable()).ToArray();

        result.Select(order => order.Id).Should().ContainInOrder(higherTotal.Id, lowerTotal.Id);
        specification.Skip.Should().Be(5);
        specification.Take.Should().Be(5);
    }
}
