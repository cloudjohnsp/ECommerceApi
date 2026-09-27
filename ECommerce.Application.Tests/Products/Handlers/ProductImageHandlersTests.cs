using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Storage;
using ECommerce.Application.Products;
using ECommerce.Application.Products.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Results;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Products.Handlers;

public sealed class ProductImageHandlersTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IProductImageRepository> _images = new();
    private readonly Mock<IProductImageStorage> _storage = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Upload_WithValidPng_StoresBlobAndMetadata()
    {
        var product = Product.Create("Mouse", "Wireless", 100m, 2).Value!;
        _products.Setup(repository => repository.GetByIdAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _products.Setup(repository => repository.GetByIdForUpdateAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _storage.Setup(storage => storage.UploadAsync(
                product.Id, It.IsAny<Stream>(), "image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StoredProductImage>.Success(new StoredProductImage(
                $"products/{product.Id:N}/image.png",
                $"https://storage.example/products/{product.Id:N}/image.png")));
        var handler = CreateUploadHandler();
        await using var content = CreatePngStream();

        var result = await handler.Handle(
            new UploadProductImageCommand(
                product.Id, content, "../image.png", "image/png", content.Length),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FileName.Should().Be("image.png");
        _images.Verify(repository => repository.AddAsync(
            It.Is<ProductImage>(image => image.ProductId == product.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unit => unit.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unit => unit.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(storage => storage.DeleteAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_WhenSignatureDoesNotMatch_DoesNotCallStorage()
    {
        var product = Product.Create("Mouse", "Wireless", 100m, 2).Value!;
        _products.Setup(repository => repository.GetByIdAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        var handler = CreateUploadHandler();
        await using var content = new MemoryStream("not an image"u8.ToArray());

        var result = await handler.Handle(
            new UploadProductImageCommand(
                product.Id, content, "image.png", "image/png", content.Length),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Image content does not match the declared content type.");
        _storage.Verify(storage => storage.UploadAsync(
            It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_WhenCommitFails_DeletesUploadedBlob()
    {
        var product = Product.Create("Mouse", "Wireless", 100m, 2).Value!;
        var storageKey = $"products/{product.Id:N}/image.png";
        _products.Setup(repository => repository.GetByIdAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _products.Setup(repository => repository.GetByIdForUpdateAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _storage.Setup(storage => storage.UploadAsync(
                product.Id, It.IsAny<Stream>(), "image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StoredProductImage>.Success(new StoredProductImage(
                storageKey,
                $"https://storage.example/{storageKey}")));
        _storage.Setup(storage => storage.DeleteAsync(storageKey, CancellationToken.None))
            .ReturnsAsync(Result.Success());
        _unitOfWork.Setup(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));
        var handler = CreateUploadHandler();
        await using var content = CreatePngStream();

        var action = () => handler.Handle(
            new UploadProductImageCommand(
                product.Id, content, "image.png", "image/png", content.Length),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        _storage.Verify(storage => storage.DeleteAsync(storageKey, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Upload_WhenProductIsDeactivatedDuringBlobUpload_DeletesUploadedBlob()
    {
        var product = Product.Create("Mouse", "Wireless", 100m, 2).Value!;
        var storageKey = $"products/{product.Id:N}/image.png";
        _products.Setup(repository => repository.GetByIdAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _products.Setup(repository => repository.GetByIdForUpdateAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        _storage.Setup(storage => storage.UploadAsync(
                product.Id, It.IsAny<Stream>(), "image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StoredProductImage>.Success(new StoredProductImage(
                storageKey,
                $"https://storage.example/{storageKey}")));
        _storage.Setup(storage => storage.DeleteAsync(storageKey, CancellationToken.None))
            .ReturnsAsync(Result.Success());
        var handler = CreateUploadHandler();
        await using var content = CreatePngStream();

        var result = await handler.Handle(
            new UploadProductImageCommand(
                product.Id, content, "image.png", "image/png", content.Length),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Product is no longer available.");
        _images.Verify(repository => repository.AddAsync(
            It.IsAny<ProductImage>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unit => unit.RollbackTransactionAsync(
            CancellationToken.None), Times.Once);
        _storage.Verify(storage => storage.DeleteAsync(
            storageKey, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GetImages_WhenProductExists_MapsRepositoryImages()
    {
        var product = Product.Create("Mouse", "Wireless", 100m, 2).Value!;
        var image = ProductImage.Create(
            product.Id,
            $"products/{product.Id:N}/image.webp",
            $"https://storage.example/products/{product.Id:N}/image.webp",
            "image.webp",
            "image/webp",
            128).Value!;
        _products.Setup(repository => repository.GetByIdAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _images.Setup(repository => repository.GetByProductIdAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([image]);
        var handler = new GetProductImagesHandler(_products.Object, _images.Object);

        var result = await handler.Handle(new GetProductImagesQuery(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Url.Should().Be(image.Url);
    }

    private UploadProductImageHandler CreateUploadHandler() => new(
        _products.Object,
        _images.Object,
        _storage.Object,
        _unitOfWork.Object);

    private static MemoryStream CreatePngStream() => new(
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0]);
}
