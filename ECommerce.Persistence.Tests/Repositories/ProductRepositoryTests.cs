using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ECommerce.Application.Products;
using ECommerce.Application.Products.Specifications;

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

        var persisted = await context.Products.AsNoTracking().Include(product => product.Inventory).SingleAsync();
        persisted.Name.Should().Be("Mouse");
        persisted.Price.Should().Be(199.90m);
        persisted.AvailableStock.Should().Be(20);
    }

    [Fact]
    public async Task GetByIdAsync_LoadsInventoryWithReservations()
    {
        await using var context = CreateContext();
        var product = CreateProduct();
        product.ReserveStock(2).IsSuccess.Should().BeTrue();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new ProductRepository(context).GetByIdAsync(product.Id);

        result.Should().NotBeNull();
        result!.AvailableStock.Should().Be(3);
        result.ReleaseReservedStock(2).IsSuccess.Should().BeTrue();
        result.AvailableStock.Should().Be(5);
    }

    [Fact]
    public async Task SearchAsync_FiltersProductsAndAppliesPagination()
    {
        await using var context = CreateContext();
        context.Products.AddRange(
            CreateProduct("Basic Mouse", 50),
            CreateProduct("Gaming Mouse", 150),
            CreateProduct("Notebook", 5000),
            CreateProduct("Premium Mouse", 300));
        await context.SaveChangesAsync();
        var repository = new ProductRepository(context);
        var specification = new ProductCatalogSpecification(new GetProductsQuery(
            Search: "mouse",
            MinPrice: 100,
            SortBy: "price",
            Descending: true,
            Page: 1,
            PageSize: 1));

        var result = await repository.SearchAsync(specification);

        result.TotalCount.Should().Be(2);
        result.Items.Should().ContainSingle().Which.Name.Should().Be("Premium Mouse");
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_WithCategory_ReturnsOnlyProductsInCategory()
    {
        await using var context = CreateContext();
        var category = Category.Create("Accessories").Value!;
        var categorized = Product.Create("Mouse", "Wireless", 150, 5, category.Id).Value!;
        var uncategorized = CreateProduct("Notebook", 5000);
        context.Categories.Add(category);
        context.Products.AddRange(categorized, uncategorized);
        await context.SaveChangesAsync();
        var repository = new ProductRepository(context);
        var specification = new ProductCatalogSpecification(
            new GetProductsQuery(CategoryId: category.Id));

        var result = await repository.SearchAsync(specification);

        result.Items.Should().ContainSingle().Which.Id.Should().Be(categorized.Id);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Product CreateProduct(string name = "Notebook", decimal price = 100)
    {
        var result = Product.Create(name, "Description", price, 5);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }
}
