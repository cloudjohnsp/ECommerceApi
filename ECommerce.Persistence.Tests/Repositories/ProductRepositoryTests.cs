using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class ProductRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsProduct()
    {
        await using var context = CreateContext();
        var repository = new ProductRepository(context);
        var product = CreateProduct();

        await repository.AddAsync(product);
        await context.SaveChangesAsync();

        var persisted = await context.Products.SingleAsync();
        persisted.Id.Should().Be(product.Id);
        persisted.Name.Should().Be(product.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingActiveProduct_ReturnsProduct()
    {
        await using var context = CreateContext();
        var product = CreateProduct();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        var repository = new ProductRepository(context);

        var result = await repository.GetByIdAsync(product.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveProductsOrderedByName()
    {
        await using var context = CreateContext();
        var notebook = CreateProduct("Notebook");
        var mouse = CreateProduct("Mouse");
        var inactive = CreateProduct("Camera");
        inactive.Deactivate();
        context.Products.AddRange(notebook, mouse, inactive);
        await context.SaveChangesAsync();
        var repository = new ProductRepository(context);

        var result = await repository.GetAllAsync();

        result.Select(x => x.Name).Should().ContainInOrder("Mouse", "Notebook");
        result.Should().NotContain(x => x.Id == inactive.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithDeactivatedProduct_ReturnsNull()
    {
        await using var context = CreateContext();
        var product = CreateProduct();
        product.Deactivate();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        var repository = new ProductRepository(context);

        var result = await repository.GetByIdAsync(product.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Update_PersistsChanges()
    {
        await using var context = CreateContext();
        var product = CreateProduct();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        product.Update("Mouse", "Wireless mouse", 199.90m, 20);
        var repository = new ProductRepository(context);

        repository.Update(product);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.Products.AsNoTracking().SingleAsync();
        persisted.Name.Should().Be("Mouse");
        persisted.Price.Should().Be(199.90m);
        persisted.Stock.Should().Be(20);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Product CreateProduct(string name = "Notebook")
    {
        var result = Product.Create(name, "Description", 100, 5);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }
}
