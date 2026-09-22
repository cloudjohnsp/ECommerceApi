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
        result.Value.AvailableStock.Should().Be(10);
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
        product.AvailableStock.Should().Be(25);
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
        product.AvailableStock.Should().Be(10);
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

    [Fact]
    public void ReserveAndReduceStock_ConsumesOnlyRequestedReservation()
    {
        var product = ProductFactory.Create(stock: 10);
        product.ReserveStock(4).IsSuccess.Should().BeTrue();
        product.ReserveStock(2).IsSuccess.Should().BeTrue();
        product.AvailableStock.Should().Be(4);
        product.ReduceStock(4).IsSuccess.Should().BeTrue();
        product.AvailableStock.Should().Be(4);
        product.ReleaseReservedStock(2).IsSuccess.Should().BeTrue();
        product.AvailableStock.Should().Be(6);
    }

    [Fact]
    public void ReserveStock_WithInsufficientAvailableQuantity_PreservesStock()
    {
        var product = ProductFactory.Create(stock: 2);
        product.ReserveStock(1).IsSuccess.Should().BeTrue();
        product.ReserveStock(2).IsFailure.Should().BeTrue();
        product.AvailableStock.Should().Be(1);
    }

    [Fact]
    public void RestoreStock_IncreasesStock()
    {
        var product = ProductFactory.Create(stock: 2);
        product.RestoreStock(3).IsSuccess.Should().BeTrue();
        product.AvailableStock.Should().Be(5);
    }

    [Fact]
    public void Update_StockBelowReservedQuantity_DoesNotMutateProduct()
    {
        var product = ProductFactory.Create(stock: 10);
        product.ReserveStock(6).IsSuccess.Should().BeTrue();

        var result = product.Update("Changed", "Changed", 100m, 5);

        result.IsFailure.Should().BeTrue();
        product.Name.Should().Be("Notebook");
        product.AvailableStock.Should().Be(4);
    }
}
