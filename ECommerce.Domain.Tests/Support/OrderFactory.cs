using ECommerce.Domain.Entities;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Support;

public static class OrderFactory
{
    public static Order Create(Guid? customerId = null, bool withItem = true)
    {
        var result = Order.Create(customerId ?? Guid.NewGuid());
        result.IsSuccess.Should().BeTrue();
        if (withItem)
            result.Value!.AddItem(Guid.NewGuid(), "Notebook", 100m, 2).IsSuccess.Should().BeTrue();
        return result.Value!;
    }
}
