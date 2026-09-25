using ECommerce.Application.Products;
using ECommerce.Application.Products.Validators;
using ECommerce.Domain.Entities;
using FluentAssertions;

namespace ECommerce.Application.Tests.Products.Validators;

public sealed class ProductImageValidatorsTests
{
    [Fact]
    public async Task Upload_WithSupportedImage_IsValid()
    {
        var validator = new UploadProductImageValidator();
        await using var content = new MemoryStream([1]);

        var result = await validator.ValidateAsync(new UploadProductImageCommand(
            Guid.NewGuid(), content, "image.webp", "image/webp", content.Length));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("application/pdf", 1024)]
    [InlineData("image/png", 0)]
    [InlineData("image/png", ProductImage.MaximumSizeBytes + 1)]
    public async Task Upload_WithUnsupportedTypeOrSize_IsInvalid(string contentType, long sizeBytes)
    {
        var validator = new UploadProductImageValidator();
        await using var content = new MemoryStream([1]);

        var result = await validator.ValidateAsync(new UploadProductImageCommand(
            Guid.NewGuid(), content, "image.png", contentType, sizeBytes));

        result.IsValid.Should().BeFalse();
    }
}
