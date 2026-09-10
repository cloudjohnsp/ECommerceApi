using ECommerce.Application.Products;
using ECommerce.Application.Products.Validators;
using FluentAssertions;

namespace ECommerce.Application.Tests.Products.Validators;

public sealed class ProductValidatorsTests
{
    [Fact]
    public async Task Create_WithValidCommand_IsValid()
    {
        var validator = new CreateProductValidator();

        var result = await validator.ValidateAsync(new CreateProductCommand("Notebook", "Description", 100, 1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithInvalidFields_ReturnsAllRelevantErrors()
    {
        var validator = new CreateProductValidator();

        var result = await validator.ValidateAsync(new CreateProductCommand("", new string('x', 2001), 0, -1));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(x => x.PropertyName).Should().Contain(["Name", "Description", "Price", "Stock"]);
    }

    [Fact]
    public async Task Update_WithEmptyId_IsInvalid()
    {
        var validator = new UpdateProductValidator();

        var result = await validator.ValidateAsync(new UpdateProductCommand(Guid.Empty, "Product", "Description", 1, 0));

        result.Errors.Should().Contain(x => x.PropertyName == "ProductId");
    }
}
