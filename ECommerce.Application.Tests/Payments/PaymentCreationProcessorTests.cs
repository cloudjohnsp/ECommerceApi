using System.Text.Json;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Payments;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Payments;

public sealed class PaymentCreationProcessorTests
{
    private readonly Mock<IOutboxMessageRepository> _outbox = new();
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Process_PendingIntention_RegistersExternalPaymentAndMarksMessageProcessed()
    {
        var (payment, intention, gatewayRequest) = SetupPendingIntention();
        _gateway.Setup(x => x.CreateAsync(gatewayRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayPayment>.Success(new GatewayPayment("pay_123", "pending")));
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(intention.Id);

        result.IsSuccess.Should().BeTrue();
        payment.ExternalPaymentId.Should().Be("pay_123");
        intention.Status.Should().Be(OutBoxMessageStatus.Processed);
        _payments.Verify(x => x.Update(payment), Times.Once);
        _outbox.Verify(x => x.Update(intention), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WhenGatewayFails_LeavesIntentionPendingForRetry()
    {
        var (_, intention, gatewayRequest) = SetupPendingIntention();
        _gateway.Setup(x => x.CreateAsync(gatewayRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayPayment>.Failure("Gateway unavailable."));
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(intention.Id);

        result.IsFailure.Should().BeTrue();
        intention.Status.Should().Be(OutBoxMessageStatus.Pending);
        _unitOfWork.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _outbox.Verify(x => x.Update(It.IsAny<OutboxMessage>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenPaymentAlreadyHasExternalId_DoesNotCallGatewayAndRepairsOutboxStatus()
    {
        var (payment, intention, _) = SetupPendingIntention();
        payment.RegisterExternalPayment("pay_123");
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(intention.Id);

        result.IsSuccess.Should().BeTrue();
        intention.Status.Should().Be(OutBoxMessageStatus.Processed);
        _gateway.Verify(x => x.CreateAsync(
            It.IsAny<CreateGatewayPayment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_ProcessedIntention_IsIdempotent()
    {
        var (payment, intention, _) = SetupPendingIntention();
        payment.RegisterExternalPayment("pay_123");
        intention.MarkProcessed();
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(intention.Id);

        result.IsSuccess.Should().BeTrue();
        _gateway.Verify(x => x.CreateAsync(
            It.IsAny<CreateGatewayPayment>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenPaymentAdvancesDuringGatewayCall_PreservesLockedState()
    {
        var (stalePayment, intention, gatewayRequest) = SetupPendingIntention();
        var currentPayment = Payment.Create(
            stalePayment.OrderId,
            stalePayment.Amount,
            stalePayment.Provider).Value!;
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(currentPayment, stalePayment.Id);
        currentPayment.MarkAsPaid("pay_123");
        _payments.Setup(x => x.GetByIdForUpdateAsync(
                stalePayment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentPayment);
        _outbox.Setup(x => x.GetByIdForUpdateAsync(
                intention.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(intention);
        _gateway.Setup(x => x.CreateAsync(gatewayRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GatewayPayment>.Success(new GatewayPayment("pay_123", "pending")));
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(intention.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(currentPayment);
        currentPayment.Status.Should().Be(PaymentStatus.Paid);
        currentPayment.PaidAt.Should().NotBeNull();
        _payments.Verify(x => x.Update(currentPayment), Times.Once);
        _payments.Verify(x => x.Update(stalePayment), Times.Never);
        intention.Status.Should().Be(OutBoxMessageStatus.Processed);
    }

    private (Payment Payment, OutboxMessage Intention, CreateGatewayPayment GatewayRequest) SetupPendingIntention()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "ECommercePayment").Value!;
        var gatewayRequest = new CreateGatewayPayment(payment.Id, payment.OrderId, payment.Amount, "BRL");
        var intention = new OutboxMessage(
            payment.Id,
            OutBoxMessageType.PaymentCreationRequested,
            JsonSerializer.Serialize(gatewayRequest));
        _outbox.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(intention);
        _payments.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _outbox.Setup(x => x.GetByIdForUpdateAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(intention);
        _payments.Setup(x => x.GetByIdForUpdateAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        return (payment, intention, gatewayRequest);
    }

    private PaymentCreationProcessor CreateProcessor() => new(
        _outbox.Object,
        _payments.Object,
        _gateway.Object,
        _unitOfWork.Object);
}
