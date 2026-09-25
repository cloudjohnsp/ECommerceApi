using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class ProductImageRepositoryTests
{
    [Fact]
    public async Task GetByProductIdAsync_ReturnsOnlyRequestedProductImagesInCreationOrder()
    {
        await using var context = CreateContext();
        var firstProduct = Product.Create("Mouse", "Wireless", 100m, 2).Value!;
        var secondProduct = Product.Create("Keyboard", "Mechanical", 200m, 2).Value!;
        var firstImage = CreateImage(firstProduct.Id, "first.webp");
        await Task.Delay(1);
        var secondImage = CreateImage(firstProduct.Id, "second.webp");
        var otherImage = CreateImage(secondProduct.Id, "other.webp");
        context.Products.AddRange(firstProduct, secondProduct);
        context.ProductImages.AddRange(secondImage, otherImage, firstImage);
        await context.SaveChangesAsync();
        var repository = new ProductImageRepository(context);

        var result = await repository.GetByProductIdAsync(firstProduct.Id);

        result.Select(image => image.FileName).Should().ContainInOrder("first.webp", "second.webp");
    }

    private static ProductImage CreateImage(Guid productId, string fileName) =>
        ProductImage.Create(
            productId,
            $"products/{productId:N}/{fileName}",
            $"https://storage.example/products/{productId:N}/{fileName}",
            fileName,
            "image/webp",
            1024).Value!;

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
