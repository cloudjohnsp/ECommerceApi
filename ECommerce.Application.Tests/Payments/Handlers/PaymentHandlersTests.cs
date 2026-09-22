using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Payments;
using ECommerce.Application.Payments.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using ECommerce.Shared.Results;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Payments.Handlers;

public sealed class PaymentHandlersTests
{
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IPaymentWebhookSignatureVerifier> _signatureVerifier = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Create_WithPendingOrder_CreatesGatewayPaymentAndStoresExternalId()
    {
        var order = OrderFactory.Create();
        _orders.Setup(x => x.GetByIdForUpdateAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _gateway.Setup(x => x.CreateAsync(It.IsAny<CreateGatewayPayment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayPayment>.Success(new GatewayPayment("pay_123", "pending")));
        var handler = new CreatePaymentHandler(
            _orders.Object, _payments.Object, _gateway.Object, _unitOfWork.Object);

        var result = await handler.Handle(new CreatePaymentCommand(order.Id, "BRL"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExternalPaymentId.Should().Be("pay_123");
        result.Value.Status.Should().Be(PaymentStatus.Pending);
        _payments.Verify(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.Commit(It.IsAny<CancellationToken>()), Times.Once);
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
            _orders.Object, _payments.Object, _gateway.Object, _unitOfWork.Object);

        var result = await handler.Handle(new CreatePaymentCommand(order.Id, "BRL"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ExternalPaymentId.Should().Be("pay_123");
        _gateway.Verify(
            x => x.CreateAsync(It.IsAny<CreateGatewayPayment>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
        _unitOfWork.Object);
}
