using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;
using ECommerce.Application.Abstractions.Specifications;
using ECommerce.Shared.Pagination;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Tests.Orders.Handlers;

public sealed class OrderHandlersTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<IOutboxMessageRepository> _outboxMessages = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IProductCache> _productCache = new();

    [Fact]
    public async Task Create_WithValidData_CreatesOrderAndDecreasesStock()
    {
        var customer = UserFactory.Create();
        var product = ProductFactory.Create(stock: 10);
        _users.Setup(x => x.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _products.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new CreateOrderHandler(
            _orders.Object, _users.Object, _products.Object, _outboxMessages.Object,
            _unitOfWork.Object, _productCache.Object);

        var result = await handler.Handle(
            new CreateOrderCommand(customer.Id, [new CreateOrderItem(product.Id, 3)]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Total.Should().Be(product.Price * 3);
        product.AvailableStock.Should().Be(7);
        _outboxMessages.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.OrderCreated &&
                message.Payload.Contains(result.Value.Id.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
        _outboxMessages.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.StockUpdated &&
                message.Payload.Contains(product.Id.ToString()) &&
                message.Payload.Contains(StockUpdateReasons.Reserved)),
            It.IsAny<CancellationToken>()), Times.Once);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _productCache.Verify(x => x.RemoveAsync(product.Id, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Create_WithUnknownCustomer_ReturnsFailureWithoutChangingStock()
    {
        var handler = new CreateOrderHandler(
            _orders.Object, _users.Object, _products.Object, _outboxMessages.Object,
            _unitOfWork.Object, _productCache.Object);

        var result = await handler.Handle(
            new CreateOrderCommand(Guid.NewGuid(), [new CreateOrderItem(Guid.NewGuid(), 1)]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _products.Verify(x => x.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WithInsufficientStock_ReturnsFailureWithoutDecreasingStock()
    {
        var customer = UserFactory.Create();
        var product = ProductFactory.Create(stock: 2);
        _users.Setup(x => x.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        _products.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new CreateOrderHandler(
            _orders.Object, _users.Object, _products.Object, _outboxMessages.Object,
            _unitOfWork.Object, _productCache.Object);

        var result = await handler.Handle(
            new CreateOrderCommand(customer.Id, [new CreateOrderItem(product.Id, 3)]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        product.AvailableStock.Should().Be(2);
        _orders.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenInfrastructureFails_RollsBackAndPropagatesException()
    {
        var customerId = Guid.NewGuid();
        const string sensitiveInfrastructureDetail = "server=internal-db; password=secret";
        _users.Setup(x => x.GetByIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(sensitiveInfrastructureDetail));
        var handler = new CreateOrderHandler(
            _orders.Object, _users.Object, _products.Object, _outboxMessages.Object,
            _unitOfWork.Object, _productCache.Object);

        var action = () => handler.Handle(
            new CreateOrderCommand(customerId, [new CreateOrderItem(Guid.NewGuid(), 1)]),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(sensitiveInfrastructureDetail);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(CancellationToken.None), Times.Once);
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
    public async Task GetById_ForAnotherCustomer_ReturnsNotFound()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var handler = new GetOrderByIdHandler(_orders.Object);

        var result = await handler.Handle(
            new GetOrderByIdQuery(order.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Order not found.");
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
    public async Task GetAll_WithCustomerScope_UsesCustomerFilteredRepositoryQuery()
    {
        var customerId = Guid.NewGuid();
        _orders.Setup(x => x.GetByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([OrderFactory.Create(customerId)]);
        var handler = new GetOrdersHandler(_orders.Object);

        var result = await handler.Handle(new GetOrdersQuery(customerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.CustomerId.Should().Be(customerId);
        _orders.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Search_ReturnsMappedPagedOrders()
    {
        var customerId = Guid.NewGuid();
        var order = OrderFactory.Create(customerId);
        _orders.Setup(repository => repository.SearchAsync(
                It.IsAny<ISpecification<Order>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Order>([order], 2, 10, 13));
        var handler = new SearchOrdersHandler(_orders.Object);

        var result = await handler.Handle(
            new SearchOrdersQuery(customerId, Page: 2, PageSize: 10),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(13);
        result.Value.Items.Should().ContainSingle().Which.CustomerId.Should().Be(customerId);
    }

    [Fact]
    public async Task AddItem_ToPendingOrder_ReservesStockAndPublishesUpdatedEvent()
    {
        var order = OrderFactory.Create();
        var product = ProductFactory.Create(stock: 10);
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _products.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        var handler = new AddOrderItemHandler(
            _orders.Object,
            _products.Object,
            _outboxMessages.Object,
            _unitOfWork.Object,
            _productCache.Object);

        var result = await handler.Handle(
            new AddOrderItemCommand(order.Id, product.Id, 2, order.CustomerId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        product.AvailableStock.Should().Be(8);
        _outboxMessages.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message => message.Type == OutBoxMessageType.OrderUpdated),
            It.IsAny<CancellationToken>()), Times.Once);
        _outboxMessages.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.StockUpdated &&
                message.Payload.Contains(StockUpdateReasons.Reserved)),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _productCache.Verify(x => x.RemoveAsync(product.Id, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AddItem_ForAnotherCustomer_ReturnsNotFoundWithoutReservingStock()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var handler = new AddOrderItemHandler(
            _orders.Object,
            _products.Object,
            _outboxMessages.Object,
            _unitOfWork.Object,
            _productCache.Object);

        var result = await handler.Handle(
            new AddOrderItemCommand(order.Id, Guid.NewGuid(), 1, Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Order not found.");
        _products.Verify(x => x.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItem_WithInsufficientStock_RollsBackWithoutChangingOrder()
    {
        var order = OrderFactory.Create();
        var product = ProductFactory.Create(stock: 1);
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _products.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        var handler = new AddOrderItemHandler(
            _orders.Object,
            _products.Object,
            _outboxMessages.Object,
            _unitOfWork.Object,
            _productCache.Object);

        var result = await handler.Handle(
            new AddOrderItemCommand(order.Id, product.Id, 2, order.CustomerId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        order.Items.Should().ContainSingle();
        product.AvailableStock.Should().Be(1);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateToPaid_WithApprovedPayment_ConsumesReservationAndCommits()
    {
        var acquiredLocks = new List<string>();
        var product = ProductFactory.Create(stock: 10);
        product.ReserveStock(2);
        var order = Order.Create(Guid.NewGuid()).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 2);
        var payment = Payment.Create(order.Id, order.Total, "ECommercePayment").Value!;
        payment.MarkAsPaid("pay_123");
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .Callback(() => acquiredLocks.Add("order"))
            .ReturnsAsync(order);
        _payments.Setup(x => x.GetByOrderIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .Callback(() => acquiredLocks.Add("payment"))
            .ReturnsAsync(payment);
        _products.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        var handler = new UpdateOrderHandler(
            _orders.Object,
            _payments.Object,
            _products.Object,
            _outboxMessages.Object,
            _unitOfWork.Object,
            _productCache.Object);

        var result = await handler.Handle(new UpdateOrderCommand(order.Id, OrderStatus.Paid), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        acquiredLocks.Should().Equal("payment", "order");
        order.Status.Should().Be(OrderStatus.Paid);
        _outboxMessages.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message => message.Type == OutBoxMessageType.OrderPaid),
            It.IsAny<CancellationToken>()), Times.Once);
        _outboxMessages.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.StockUpdated &&
                message.Payload.Contains(StockUpdateReasons.ReservationConsumed)),
            It.IsAny<CancellationToken>()), Times.Once);
        product.ReleaseReservedStock(2).IsFailure.Should().BeTrue();
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _productCache.Verify(x => x.RemoveAsync(product.Id, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task UpdateToPaid_WithoutApprovedPayment_RollsBackWithoutChangingOrder()
    {
        var order = OrderFactory.Create();
        var payment = Payment.Create(order.Id, order.Total, "ECommercePayment").Value!;
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _payments.Setup(x => x.GetByOrderIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var handler = new UpdateOrderHandler(
            _orders.Object,
            _payments.Object,
            _products.Object,
            _outboxMessages.Object,
            _unitOfWork.Object,
            _productCache.Object);

        var result = await handler.Handle(
            new UpdateOrderCommand(order.Id, OrderStatus.Paid),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Order can only be reconciled after its payment is approved.");
        order.Status.Should().Be(OrderStatus.Pending);
        _products.Verify(x => x.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePendingOrder_CancelsOrderAndRestoresStock()
    {
        var product = ProductFactory.Create(stock: 10);
        product.ReserveStock(3);
        var orderResult = Order.Create(Guid.NewGuid());
        orderResult.Value!.AddItem(product.Id, product.Name, product.Price, 3);
        var order = orderResult.Value;
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _products.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = new DeleteOrderHandler(
            _orders.Object, _products.Object, _outboxMessages.Object,
            _unitOfWork.Object, _productCache.Object);

        var result = await handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        product.AvailableStock.Should().Be(10);
        _outboxMessages.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message => message.Type == OutBoxMessageType.OrderCancelled),
            It.IsAny<CancellationToken>()), Times.Once);
        _outboxMessages.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.StockUpdated &&
                message.Payload.Contains(StockUpdateReasons.ReservationReleased)),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _productCache.Verify(x => x.RemoveAsync(product.Id, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task DeletePaidOrder_ReturnsFailureWithoutRestoringStock()
    {
        var order = OrderFactory.Create();
        order.MarkAsPaid();
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var handler = new DeleteOrderHandler(
            _orders.Object, _products.Object, _outboxMessages.Object,
            _unitOfWork.Object, _productCache.Object);

        var result = await handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _products.Verify(x => x.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteOrder_ForAnotherCustomer_ReturnsNotFoundWithoutChangingOrder()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var handler = new DeleteOrderHandler(
            _orders.Object, _products.Object, _outboxMessages.Object,
            _unitOfWork.Object, _productCache.Object);

        var result = await handler.Handle(
            new DeleteOrderCommand(order.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Order not found.");
        order.Status.Should().Be(OrderStatus.Pending);
        _products.Verify(x => x.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePendingOrder_WhenProductCannotBeLoaded_RollsBackTransaction()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var handler = new DeleteOrderHandler(
            _orders.Object, _products.Object, _outboxMessages.Object,
            _unitOfWork.Object, _productCache.Object);

        var result = await handler.Handle(new DeleteOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
