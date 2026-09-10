using ECommerce.Domain.Entities;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public sealed class ProductTests
{
    [Fact]
    public void Create_WithValidData_ReturnsActiveProduct()
    {
        var result = Product.Create("  Notebook  ", "  Gaming notebook  ", 4999.90m, 10);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().NotBeEmpty();
        result.Value.Name.Should().Be("Notebook");
        result.Value.Description.Should().Be("Gaming notebook");
        result.Value.Price.Should().Be(4999.90m);
        result.Value.Stock.Should().Be(10);
        result.Value.IsActive.Should().BeTrue();
        result.Value.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("", "Description", 10, 1, "Product name must contain between 1 and 150 characters.")]
    [InlineData("Product", "Description", 0, 1, "Product price must be greater than zero.")]
    [InlineData("Product", "Description", 10, -1, "Product stock cannot be negative.")]
    public void Create_WithInvalidData_ReturnsFailure(string name, string description, decimal price, int stock, string error)
    {
        var result = Product.Create(name, description, price, stock);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(error);
    }

    [Fact]
    public void Update_WithValidData_ChangesProductAndTimestamp()
    {
        var product = ProductFactory.Create();

        var result = product.Update("Mouse", "Wireless mouse", 199.90m, 25);

        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("Mouse");
        product.Description.Should().Be("Wireless mouse");
        product.Price.Should().Be(199.90m);
        product.Stock.Should().Be(25);
        product.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Update_WithInvalidData_DoesNotChangeProduct()
    {
        var product = ProductFactory.Create();

        var result = product.Update("", "Changed", -1, -1);

        result.IsFailure.Should().BeTrue();
        product.Name.Should().Be("Notebook");
        product.Price.Should().Be(4999.90m);
        product.Stock.Should().Be(10);
        product.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Deactivate_WhenActive_PerformsSoftDelete()
    {
        var product = ProductFactory.Create();

        var result = product.Deactivate();

        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeFalse();
        product.DeactivatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        product.UpdatedAt.Should().Be(product.DeactivatedAt);
    }
}
