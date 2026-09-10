using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products;
using ECommerce.Application.Products.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Products.Handlers;

public sealed class ProductHandlersTests
{
    private readonly Mock<IProductRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Create_WithValidCommand_PersistsAndReturnsProduct()
    {
        var command = new CreateProductCommand("Notebook", "Gaming notebook", 4999.90m, 10);
        var handler = new CreateProductHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be(command.Name);
        _repository.Verify(x => x.AddAsync(It.Is<Product>(p => p.Name == command.Name), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_WhenProductExists_ReturnsProduct()
    {
        var product = ProductFactory.Create();
        _repository.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new GetProductByIdHandler(_repository.Object);

        var result = await handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task GetById_WhenProductDoesNotExist_ReturnsFailure()
    {
        var handler = new GetProductByIdHandler(_repository.Object);

        var result = await handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Product not found.");
    }

    [Fact]
    public async Task GetAll_ReturnsMappedProducts()
    {
        Product[] products = [ProductFactory.Create("Notebook"), ProductFactory.Create("Mouse")];
        _repository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);
        var handler = new GetProductsHandler(_repository.Object);

        var result = await handler.Handle(new GetProductsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(x => x.Name).Should().BeEquivalentTo("Notebook", "Mouse");
    }

    [Fact]
    public async Task Update_WhenProductExists_UpdatesAndCommits()
    {
        var product = ProductFactory.Create();
        _repository.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new UpdateProductHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new UpdateProductCommand(product.Id, "Mouse", "Wireless", 150, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Mouse");
        _repository.Verify(x => x.Update(product), Times.Once);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenProductDoesNotExist_DoesNotCommit()
    {
        var handler = new UpdateProductHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new UpdateProductCommand(Guid.NewGuid(), "Mouse", "Wireless", 150, 20), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _repository.Verify(x => x.Update(It.IsAny<Product>()), Times.Never);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenProductExists_DeactivatesAndCommits()
    {
        var product = ProductFactory.Create();
        _repository.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new DeleteProductHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeFalse();
        _repository.Verify(x => x.Update(product), Times.Once);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenProductDoesNotExist_ReturnsFailureWithoutCommit()
    {
        var handler = new DeleteProductHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new DeleteProductCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }
}
