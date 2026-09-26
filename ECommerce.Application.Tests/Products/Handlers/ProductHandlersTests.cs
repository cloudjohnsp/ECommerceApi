using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products;
using ECommerce.Application.Products.Dtos;
using ECommerce.Application.Products.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;
using System.Text.Json;
using ECommerce.Application.Abstractions.Specifications;
using ECommerce.Shared.Pagination;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Tests.Products.Handlers;

public sealed class ProductHandlersTests
{
    private readonly Mock<IProductRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IProductCache> _cache = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IOutboxMessageRepository> _outbox = new();

    [Fact]
    public async Task Create_WithValidCommand_PersistsAndReturnsProduct()
    {
        var command = new CreateProductCommand("Notebook", "Gaming notebook", 4999.90m, 10);
        var handler = new CreateProductHandler(
            _repository.Object, _outbox.Object, _unitOfWork.Object, _cache.Object, _categories.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be(command.Name);
        result.Value.AvailableStock.Should().Be(10);
        var json = JsonSerializer.Serialize(result.Value);
        json.Should().Contain("AvailableStock");
        json.Should().NotContain("\"Stock\"");
        json.Should().NotContain("ReservedStock");
        _repository.Verify(x => x.AddAsync(It.Is<Product>(p => p.Name == command.Name), It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(repository => repository.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.StockUpdated &&
                message.Payload.Contains(StockUpdateReasons.Created) &&
                message.Payload.Contains("\"AvailableStock\":10")),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(x => x.SetAsync(
            It.Is<ProductDto>(product => product.Id == result.Value.Id),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Create_WithCategory_AssignsExistingCategory()
    {
        var category = Category.Create("Computers").Value!;
        _categories.Setup(repository => repository.GetByIdAsync(
                category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var command = new CreateProductCommand("Notebook", "Gaming", 4999.90m, 10, category.Id);
        var handler = new CreateProductHandler(
            _repository.Object, _outbox.Object, _unitOfWork.Object, _cache.Object, _categories.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public async Task Create_WithUnknownCategory_ReturnsFailureWithoutPersisting()
    {
        var handler = new CreateProductHandler(
            _repository.Object, _outbox.Object, _unitOfWork.Object, _cache.Object, _categories.Object);

        var result = await handler.Handle(
            new CreateProductCommand("Notebook", "Gaming", 4999.90m, 10, Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Category not found.");
        _repository.Verify(repository => repository.AddAsync(
            It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        _outbox.Verify(repository => repository.AddAsync(
            It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_WhenProductExists_ReturnsProduct()
    {
        var product = ProductFactory.Create();
        _repository.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new GetProductByIdHandler(_repository.Object, _cache.Object);

        var result = await handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(product.Id);
        _cache.Verify(x => x.SetAsync(
            It.Is<ProductDto>(cached => cached.Id == product.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_WhenProductIsCached_DoesNotQueryRepository()
    {
        var cached = new ProductDto(
            Guid.NewGuid(), "Mouse", "Wireless", 150, 8, true,
            DateTimeOffset.UtcNow, null);
        _cache.Setup(x => x.GetAsync(cached.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);
        var handler = new GetProductByIdHandler(_repository.Object, _cache.Object);

        var result = await handler.Handle(new GetProductByIdQuery(cached.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(cached);
        _repository.Verify(
            x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetById_WhenProductDoesNotExist_ReturnsFailure()
    {
        var handler = new GetProductByIdHandler(_repository.Object, _cache.Object);

        var result = await handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Product not found.");
    }

    [Fact]
    public async Task GetAll_ReturnsMappedProducts()
    {
        Product[] products = [ProductFactory.Create("Notebook"), ProductFactory.Create("Mouse")];
        _repository.Setup(x => x.SearchAsync(
                It.IsAny<ISpecification<Product>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Product>(products, 1, 20, products.Length));
        var handler = new GetProductsHandler(_repository.Object);

        var result = await handler.Handle(new GetProductsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Select(x => x.Name).Should().BeEquivalentTo("Notebook", "Mouse");
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Update_WhenProductExists_UpdatesAndCommits()
    {
        var product = ProductFactory.Create();
        OutboxMessage? stockMessage = null;
        _repository.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _outbox.Setup(repository => repository.AddAsync(
                It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((message, _) => stockMessage = message)
            .Returns(Task.CompletedTask);
        var handler = new UpdateProductHandler(
            _repository.Object, _outbox.Object, _unitOfWork.Object, _cache.Object, _categories.Object);

        var result = await handler.Handle(new UpdateProductCommand(product.Id, "Mouse", "Wireless", 150, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Mouse");
        _repository.Verify(x => x.Update(product), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(x => x.SetAsync(
            It.Is<ProductDto>(cached => cached.Id == product.Id && cached.Name == "Mouse"),
            CancellationToken.None), Times.Once);
        _outbox.Verify(repository => repository.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.StockUpdated &&
                message.Payload.Contains(StockUpdateReasons.Adjusted)),
            It.IsAny<CancellationToken>()), Times.Once);
        var payload = JsonSerializer.Deserialize<StockUpdatedIntegrationEventPayload>(stockMessage!.Payload);
        payload.Should().NotBeNull();
        payload!.ProductId.Should().Be(product.Id);
        payload.AvailableStock.Should().Be(20);
        stockMessage.Payload.Should().NotContain("\"Stock\":");
        stockMessage.Payload.Should().NotContain("ReservedStock");
    }

    [Fact]
    public async Task Update_WithCategory_AssignsExistingCategory()
    {
        var product = ProductFactory.Create();
        var category = Category.Create("Accessories").Value!;
        _repository.Setup(repository => repository.GetByIdForUpdateAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _categories.Setup(repository => repository.GetByIdAsync(
                category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var handler = new UpdateProductHandler(
            _repository.Object, _outbox.Object, _unitOfWork.Object, _cache.Object, _categories.Object);

        var result = await handler.Handle(
            new UpdateProductCommand(product.Id, "Mouse", "Wireless", 150, 20, category.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public async Task Update_WithoutAvailableStockChange_DoesNotPublishStockEvent()
    {
        var product = ProductFactory.Create(stock: 10);
        _repository.Setup(x => x.GetByIdForUpdateAsync(
                product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        var handler = new UpdateProductHandler(
            _repository.Object, _outbox.Object, _unitOfWork.Object, _cache.Object, _categories.Object);

        var result = await handler.Handle(
            new UpdateProductCommand(product.Id, "Mouse", "Wireless", 150, 10),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _outbox.Verify(repository => repository.AddAsync(
            It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenProductDoesNotExist_DoesNotCommit()
    {
        var handler = new UpdateProductHandler(
            _repository.Object, _outbox.Object, _unitOfWork.Object, _cache.Object, _categories.Object);

        var result = await handler.Handle(new UpdateProductCommand(Guid.NewGuid(), "Mouse", "Wireless", 150, 20), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _repository.Verify(x => x.Update(It.IsAny<Product>()), Times.Never);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenProductExists_DeactivatesAndCommits()
    {
        var product = ProductFactory.Create();
        _repository.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new DeleteProductHandler(_repository.Object, _unitOfWork.Object, _cache.Object);

        var result = await handler.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeFalse();
        _repository.Verify(x => x.Update(product), Times.Once);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(x => x.RemoveAsync(product.Id, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenProductDoesNotExist_ReturnsFailureWithoutCommit()
    {
        var handler = new DeleteProductHandler(_repository.Object, _unitOfWork.Object, _cache.Object);

        var result = await handler.Handle(new DeleteProductCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }
}
