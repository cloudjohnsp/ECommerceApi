using ECommerce.Domain.Entities;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Support;

public static class ProductFactory
{
    public static Product Create(string name = "Notebook", string description = "Gaming notebook", decimal price = 4999.90m, int stock = 10)
    {
        var result = Product.Create(name, description, price, stock);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }
}
