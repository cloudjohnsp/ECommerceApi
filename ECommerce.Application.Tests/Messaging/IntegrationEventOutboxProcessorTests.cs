using ECommerce.Application.Abstractions.Messaging;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Messaging;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Messaging;

public sealed class IntegrationEventOutboxProcessorTests
{
    private readonly Mock<IOutboxMessageRepository> _outbox = new();
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Process_PendingOrderCreated_PublishesAndMarksMessageProcessed()
    {
        var message = new OutboxMessage(OutBoxMessageType.OrderCreated, "{\"orderId\":\"123\"}");
        _outbox.Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);
        _publisher.Setup(x => x.PublishAsync(It.IsAny<IntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(message.Id);

        result.IsSuccess.Should().BeTrue();
        message.Status.Should().Be(OutBoxMessageStatus.Processed);
        _publisher.Verify(x => x.PublishAsync(
            It.Is<IntegrationEvent>(integrationEvent =>
                integrationEvent.Id == message.Id &&
                integrationEvent.Type == "order.created" &&
                integrationEvent.Payload == message.Payload),
            It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(x => x.Update(message), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WhenPublicationFails_LeavesMessagePending()
    {
        var message = new OutboxMessage(OutBoxMessageType.OrderCreated, "{}");
        _outbox.Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);
        _publisher.Setup(x => x.PublishAsync(It.IsAny<IntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("RabbitMQ unavailable."));
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(message.Id);

        result.IsFailure.Should().BeTrue();
        message.Status.Should().Be(OutBoxMessageStatus.Pending);
        _unitOfWork.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(OutBoxMessageType.OrderPaid, "order.paid")]
    [InlineData(OutBoxMessageType.PaymentFailed, "payment.failed")]
    [InlineData(OutBoxMessageType.OrderRefunded, "order.refunded")]
    [InlineData(OutBoxMessageType.OrderCancelled, "order.cancelled")]
    [InlineData(OutBoxMessageType.StockUpdated, "stock.updated")]
    public async Task Process_OrderLifecycleMessage_UsesExpectedRoutingKey(
        OutBoxMessageType messageType,
        string expectedRoutingKey)
    {
        var message = new OutboxMessage(messageType, "{}");
        _outbox.Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        _publisher.Setup(x => x.PublishAsync(
                It.IsAny<IntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(message.Id);

        result.IsSuccess.Should().BeTrue();
        _publisher.Verify(x => x.PublishAsync(
            It.Is<IntegrationEvent>(integrationEvent =>
                integrationEvent.Type == expectedRoutingKey),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_PaymentCreationIntention_DoesNotPublishAsIntegrationEvent()
    {
        var message = new OutboxMessage(OutBoxMessageType.PaymentCreationRequested, "{}");
        _outbox.Setup(x => x.GetByIdAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(message.Id);

        result.IsFailure.Should().BeTrue();
        _publisher.Verify(x => x.PublishAsync(
            It.IsAny<IntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private IntegrationEventOutboxProcessor CreateProcessor() => new(
        _outbox.Object,
        _publisher.Object,
        _unitOfWork.Object);
}
