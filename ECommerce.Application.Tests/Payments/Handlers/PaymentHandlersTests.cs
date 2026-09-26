using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Payments;
using ECommerce.Application.Payments.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using ECommerce.Shared.Results;
using ECommerce.Shared.Messaging;
using FluentAssertions;
using Moq;
using System.Text.Json;

namespace ECommerce.Application.Tests.Payments.Handlers;

public sealed class PaymentHandlersTests
{
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IProductCache> _productCache = new();
    private readonly Mock<IPaymentCreationProcessor> _paymentProcessor = new();
    private readonly Mock<IPaymentWebhookSignatureVerifier> _signatureVerifier = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOutboxMessageRepository> _outbox = new();

    [Fact]
    public async Task Create_WithPendingOrder_PersistsIntentionBeforeProcessingIt()
    {
        var order = OrderFactory.Create();
        Payment? createdPayment = null;
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _payments.Setup(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((payment, _) => createdPayment = payment)
            .Returns(Task.CompletedTask);
        _paymentProcessor.Setup(x => x.ProcessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                createdPayment!.RegisterExternalPayment("pay_123");
                return Result<Payment>.Success(createdPayment);
            });
        var handler = new CreatePaymentHandler(
            _orders.Object, _payments.Object, _outbox.Object, _paymentProcessor.Object, _unitOfWork.Object);

        var result = await handler.Handle(new CreatePaymentCommand(order.Id, "BRL"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExternalPaymentId.Should().Be("pay_123");
        result.Value.Status.Should().Be(PaymentStatus.Pending);
        _payments.Verify(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(x => x.AddAsync(It.Is<OutboxMessage>(message =>
            message.Type == OutBoxMessageType.PaymentCreationRequested &&
            message.Status == OutBoxMessageStatus.Pending), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _paymentProcessor.Verify(x => x.ProcessAsync(createdPayment!.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenPaymentWasAlreadyRegistered_DoesNotCallGatewayAgain()
    {
        var order = OrderFactory.Create();
        var payment = Payment.Create(order.Id, order.Total, "ECommercePayment").Value!;
        payment.RegisterExternalPayment("pay_123");
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _payments.Setup(x => x.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        var handler = new CreatePaymentHandler(
            _orders.Object, _payments.Object, _outbox.Object, _paymentProcessor.Object, _unitOfWork.Object);

        var result = await handler.Handle(new CreatePaymentCommand(order.Id, "BRL"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExternalPaymentId.Should().Be("pay_123");
        _paymentProcessor.Verify(
            x => x.ProcessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_WhenGatewayFails_KeepsPendingOutboxIntentionForRetry()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _paymentProcessor.Setup(x => x.ProcessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Payment>.Failure("Gateway unavailable."));
        OutboxMessage? intention = null;
        _outbox.Setup(x => x.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((message, _) => intention = message)
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                intention.Should().NotBeNull();
                _paymentProcessor.Verify(x => x.ProcessAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            })
            .Returns(Task.CompletedTask);
        var handler = new CreatePaymentHandler(
            _orders.Object, _payments.Object, _outbox.Object, _paymentProcessor.Object, _unitOfWork.Object);

        var result = await handler.Handle(new CreatePaymentCommand(order.Id, "BRL"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        intention.Should().NotBeNull();
        intention!.Status.Should().Be(OutBoxMessageStatus.Pending);
        intention.Type.Should().Be(OutBoxMessageType.PaymentCreationRequested);
        var payload = JsonSerializer.Deserialize<CreateGatewayPayment>(intention.Payload);
        payload!.PaymentId.Should().Be(intention.Id);
        payload.OrderId.Should().Be(order.Id);
        payload.Amount.Should().Be(order.Total);
        payload.Currency.Should().Be("BRL");
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _paymentProcessor.Verify(x => x.ProcessAsync(intention.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenSavingIntentionFails_RollsBackAndDoesNotCallGateway()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _outbox.Setup(x => x.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Outbox write failed."));
        var handler = new CreatePaymentHandler(
            _orders.Object, _payments.Object, _outbox.Object, _paymentProcessor.Object, _unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreatePaymentCommand(order.Id, "BRL"), CancellationToken.None));

        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _paymentProcessor.Verify(x => x.ProcessAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_ForAnotherCustomersOrder_ReturnsNotFoundWithoutCreatingPayment()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var handler = new CreatePaymentHandler(
            _orders.Object, _payments.Object, _outbox.Object, _paymentProcessor.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new CreatePaymentCommand(order.Id, "BRL", Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Order not found.");
        _payments.Verify(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        _outbox.Verify(x => x.AddAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPayment_ForAnotherCustomersOrder_ReturnsNotFoundWithoutLoadingPayment()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var handler = new GetPaymentByOrderIdHandler(_payments.Object, _orders.Object);

        var result = await handler.Handle(
            new GetPaymentByOrderIdQuery(order.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Payment not found.");
        _payments.Verify(x => x.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refund_PaidPayment_RefundsOrderAndRestoresStock()
    {
        var product = ProductFactory.Create(stock: 10);
        product.ReserveStock(3);
        product.ReduceStock(3);
        var order = Order.Create(Guid.NewGuid()).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 3);
        order.MarkAsPaid();
        var payment = Payment.Create(order.Id, order.Total, "ECommercePayment").Value!;
        payment.MarkAsPaid("pay_123");
        _orders.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _payments.Setup(x => x.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _payments.Setup(x => x.GetByOrderIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _products.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(x => x.RefundAsync(It.IsAny<RefundGatewayPayment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayPayment>.Success(new GatewayPayment("pay_123", "refunded")));
        var handler = new RefundPaymentHandler(
            _orders.Object,
            _payments.Object,
            _products.Object,
            _outbox.Object,
            gateway.Object,
            _unitOfWork.Object,
            _productCache.Object);

        var result = await handler.Handle(
            new RefundPaymentCommand(order.Id, "customer_request", order.CustomerId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PaymentStatus.Refunded);
        payment.Status.Should().Be(PaymentStatus.Refunded);
        order.Status.Should().Be(OrderStatus.Refunded);
        product.AvailableStock.Should().Be(10);
        gateway.Verify(x => x.RefundAsync(
            It.Is<RefundGatewayPayment>(request =>
                request.ExternalPaymentId == "pay_123" &&
                request.Reason == "customer_request"),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message => message.Type == OutBoxMessageType.OrderRefunded),
            It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.StockUpdated &&
                message.Payload.Contains(StockUpdateReasons.Restored)),
            It.IsAny<CancellationToken>()), Times.Once);
        _productCache.Verify(x => x.RemoveAsync(product.Id, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Refund_WhenAlreadyRefunded_ReturnsSuccessWithoutCallingGatewayAgain()
    {
        var order = OrderFactory.Create();
        order.MarkAsPaid();
        order.MarkAsRefunded();
        var payment = Payment.Create(order.Id, order.Total, "ECommercePayment").Value!;
        payment.MarkAsPaid("pay_123");
        payment.MarkAsRefunded();
        _orders.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _payments.Setup(x => x.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>(MockBehavior.Strict);
        var handler = new RefundPaymentHandler(
            _orders.Object,
            _payments.Object,
            _products.Object,
            _outbox.Object,
            gateway.Object,
            _unitOfWork.Object,
            _productCache.Object);

        var result = await handler.Handle(
            new RefundPaymentCommand(order.Id, null, order.CustomerId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        gateway.VerifyNoOtherCalls();
        _unitOfWork.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_Approved_MarksPaymentAndOrderAsPaid()
    {
        var product = ProductFactory.Create(stock: 10);
        product.ReserveStock(3);
        var order = Order.Create(Guid.NewGuid()).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 3);
        var payment = Payment.Create(order.Id, order.Total, "ECommercePayment").Value!;
        payment.RegisterExternalPayment("pay_123");
        SetupWebhook(payment, order);
        _products.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = CreateWebhookHandler();

        var result = await handler.Handle(
            new ProcessPaymentWebhookCommand(
                "{\"event\":\"payment.approved\",\"data\":{\"id\":\"pay_123\",\"status\":\"approved\"}}",
                "valid"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Paid);
        order.Status.Should().Be(OrderStatus.Paid);
        product.AvailableStock.Should().Be(7);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _productCache.Verify(x => x.RemoveAsync(product.Id, CancellationToken.None), Times.Once);
        _outbox.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message => message.Type == OutBoxMessageType.OrderPaid),
            It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.StockUpdated &&
                message.Payload.Contains(StockUpdateReasons.ReservationConsumed)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_Declined_CancelsOrderAndRestoresStock()
    {
        var product = ProductFactory.Create(stock: 10);
        product.ReserveStock(3);
        var order = Order.Create(Guid.NewGuid()).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 3);
        var payment = Payment.Create(order.Id, order.Total, "ECommercePayment").Value!;
        payment.RegisterExternalPayment("pay_123");
        SetupWebhook(payment, order);
        _products.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        var handler = CreateWebhookHandler();

        var result = await handler.Handle(
            new ProcessPaymentWebhookCommand(
                "{\"event\":\"payment.declined\",\"data\":{\"id\":\"pay_123\",\"status\":\"declined\"}}",
                "valid"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        order.Status.Should().Be(OrderStatus.Cancelled);
        product.AvailableStock.Should().Be(10);
        _productCache.Verify(x => x.RemoveAsync(product.Id, CancellationToken.None), Times.Once);
        _outbox.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message => message.Type == OutBoxMessageType.PaymentFailed),
            It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.StockUpdated &&
                message.Payload.Contains(StockUpdateReasons.ReservationReleased)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_Refunded_RefundsPaymentAndOrderAndRestoresStock()
    {
        var product = ProductFactory.Create(stock: 10);
        product.ReserveStock(3);
        product.ReduceStock(3);
        var order = Order.Create(Guid.NewGuid()).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 3);
        order.MarkAsPaid();
        var payment = Payment.Create(order.Id, order.Total, "ECommercePayment").Value!;
        payment.MarkAsPaid("pay_123");
        SetupWebhook(payment, order);
        _products.Setup(x => x.GetByIdForUpdateAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        var handler = CreateWebhookHandler();

        var result = await handler.Handle(
            new ProcessPaymentWebhookCommand(
                "{\"event\":\"payment.refunded\",\"data\":{\"id\":\"pay_123\",\"status\":\"refunded\"}}",
                "valid"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        order.Status.Should().Be(OrderStatus.Refunded);
        product.AvailableStock.Should().Be(10);
        _productCache.Verify(x => x.RemoveAsync(product.Id, CancellationToken.None), Times.Once);
        _outbox.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message => message.Type == OutBoxMessageType.OrderRefunded),
            It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(x => x.AddAsync(
            It.Is<OutboxMessage>(message =>
                message.Type == OutBoxMessageType.StockUpdated &&
                message.Payload.Contains(StockUpdateReasons.Restored)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_DoesNotLoadPayment()
    {
        _signatureVerifier.Setup(x => x.IsValid(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        var handler = CreateWebhookHandler();

        var result = await handler.Handle(
            new ProcessPaymentWebhookCommand("{}", "invalid"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _payments.Verify(
            x => x.GetByExternalIdForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Webhook_WithStatusThatContradictsEvent_IsRejectedBeforeDatabaseAccess()
    {
        _signatureVerifier.Setup(x => x.IsValid(It.IsAny<string>(), "valid")).Returns(true);
        var handler = CreateWebhookHandler();

        var result = await handler.Handle(
            new ProcessPaymentWebhookCommand(
                "{\"event\":\"payment.approved\",\"data\":{\"id\":\"pay_123\",\"status\":\"declined\"}}",
                "valid"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Payment webhook event and status do not match.");
        _payments.Verify(
            x => x.GetByExternalIdForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(
            x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Webhook_WithUnsupportedEvent_IsRejectedBeforeDatabaseAccess()
    {
        _signatureVerifier.Setup(x => x.IsValid(It.IsAny<string>(), "valid")).Returns(true);
        var handler = CreateWebhookHandler();

        var result = await handler.Handle(
            new ProcessPaymentWebhookCommand(
                "{\"event\":\"payment.pending\",\"data\":{\"id\":\"pay_123\",\"status\":\"pending\"}}",
                "valid"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Unsupported payment webhook event.");
        _payments.Verify(
            x => x.GetByExternalIdForUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(
            x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Webhook_Declined_WhenOrderIsAlreadyCancelled_DoesNotRestoreStockAgain()
    {
        var order = OrderFactory.Create();
        order.Cancel();
        var payment = Payment.Create(order.Id, order.Total, "ECommercePayment").Value!;
        payment.RegisterExternalPayment("pay_123");
        SetupWebhook(payment, order);
        var handler = CreateWebhookHandler();

        var result = await handler.Handle(
            new ProcessPaymentWebhookCommand(
                "{\"event\":\"payment.declined\",\"data\":{\"id\":\"pay_123\",\"status\":\"declined\"}}",
                "valid"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        _products.Verify(
            x => x.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetupWebhook(Payment payment, Order order)
    {
        _signatureVerifier.Setup(x => x.IsValid(It.IsAny<string>(), "valid")).Returns(true);
        _payments.Setup(x => x.GetByExternalIdForUpdateAsync("pay_123", It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
    }

    private ProcessPaymentWebhookHandler CreateWebhookHandler() => new(
        _signatureVerifier.Object,
        _payments.Object,
        _orders.Object,
        _products.Object,
        _outbox.Object,
        _unitOfWork.Object,
        _productCache.Object);
}
