using ECommerce.Domain.Entities;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public sealed class ProductImageTests
{
    [Fact]
    public void Create_WithValidMetadata_NormalizesValues()
    {
        var productId = Guid.NewGuid();

        var result = ProductImage.Create(
            productId,
            " products/image.webp ",
            " https://storage.example/products/image.webp ",
            " image.webp ",
            " IMAGE/WEBP ",
            1024);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProductId.Should().Be(productId);
        result.Value.StorageKey.Should().Be("products/image.webp");
        result.Value.ContentType.Should().Be("image/webp");
        result.Value.SizeBytes.Should().Be(1024);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ProductImage.MaximumSizeBytes + 1)]
    public void Create_WithInvalidSize_ReturnsFailure(long sizeBytes)
    {
        var result = ProductImage.Create(
            Guid.NewGuid(),
            "products/image.webp",
            "https://storage.example/products/image.webp",
            "image.webp",
            "image/webp",
            sizeBytes);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(error => error.StartsWith("Image size must be"));
    }

    [Fact]
    public void Create_WithRelativeUrl_ReturnsFailure()
    {
        var result = ProductImage.Create(
            Guid.NewGuid(),
            "products/image.webp",
            "/products/image.webp",
            "image.webp",
            "image/webp",
            1024);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Image URL must be an absolute URL with at most 2048 characters.");
    }
}
