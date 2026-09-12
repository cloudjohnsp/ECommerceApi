using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Orders.Handlers;

public sealed class OrderHandlersTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Create_WithValidData_CreatesOrderAndDecreasesStock()
    {
        var customer = UserFactory.Create();
        var product = ProductFactory.Create(stock: 10);
        _users.Setup(x => x.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new CreateOrderHandler(_orders.Object, _users.Object, _products.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new CreateOrderCommand(customer.Id, [new CreateOrderItem(product.Id, 3)]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Total.Should().Be(product.Price * 3);
        product.Stock.Should().Be(7);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WithUnknownCustomer_ReturnsFailureWithoutChangingStock()
    {
        var handler = new CreateOrderHandler(_orders.Object, _users.Object, _products.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new CreateOrderCommand(Guid.NewGuid(), [new CreateOrderItem(Guid.NewGuid(), 1)]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _products.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithInsufficientStock_ReturnsFailureWithoutDecreasingStock()
    {
        var customer = UserFactory.Create();
        var product = ProductFactory.Create(stock: 2);
        _users.Setup(x => x.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new CreateOrderHandler(_orders.Object, _users.Object, _products.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new CreateOrderCommand(customer.Id, [new CreateOrderItem(product.Id, 3)]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        product.Stock.Should().Be(2);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsOrder()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var handler = new GetOrderByIdHandler(_orders.Object);

        var result = await handler.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAll_ReturnsMappedOrders()
    {
        _orders.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([OrderFactory.Create(), OrderFactory.Create()]);
        var handler = new GetOrdersHandler(_orders.Object);

        var result = await handler.Handle(new GetOrdersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateToPaid_WhenFound_MarksOrderPaidAndCommits()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var handler = new UpdateOrderHandler(_orders.Object, _unitOfWork.Object);

        var result = await handler.Handle(new UpdateOrderCommand(order.Id, OrderStatus.Paid), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePendingOrder_CancelsOrderAndRestoresStock()
    {
        var product = ProductFactory.Create(stock: 7);
        var orderResult = Order.Create(Guid.NewGuid());
        orderResult.Value!.AddItem(product.Id, product.Name, product.Price, 3);
        var order = orderResult.Value;
        _orders.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _products.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new DeleteOrderHandler(_orders.Object, _products.Object, _unitOfWork.Object);

        var result = await handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        product.Stock.Should().Be(10);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePaidOrder_ReturnsFailureWithoutRestoringStock()
    {
        var order = OrderFactory.Create();
        order.MarkAsPaid();
        _orders.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var handler = new DeleteOrderHandler(_orders.Object, _products.Object, _unitOfWork.Object);

        var result = await handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _products.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }
}
