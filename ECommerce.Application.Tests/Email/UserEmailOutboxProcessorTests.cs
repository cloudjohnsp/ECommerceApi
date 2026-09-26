using System.Text.Json;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Email;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using ECommerce.Shared.Messaging;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Email;

public sealed class UserEmailOutboxProcessorTests
{
    [Theory]
    [InlineData(
        OutBoxMessageType.EmailConfirmationRequested,
        UserEmailDeliveryType.EmailConfirmation,
        EmailDeliveryCategories.EmailConfirmation)]
    [InlineData(
        OutBoxMessageType.PasswordResetRequested,
        UserEmailDeliveryType.PasswordReset,
        EmailDeliveryCategories.PasswordReset)]
    public async Task ProcessAsync_WhenEmailIsSent_MarksMessageProcessedAndPublishesSanitizedEvent(
        OutBoxMessageType messageType,
        UserEmailDeliveryType deliveryType,
        string expectedCategory)
    {
        var delivery = new UserEmailDelivery(
            "jane@example.com", "Jane", "secret-token", deliveryType);
        var message = new OutboxMessage(
            messageType,
            JsonSerializer.Serialize(delivery));
        var repository = new Mock<IOutboxMessageRepository>();
        OutboxMessage? sentEvent = null;
        repository.Setup(item => item.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        repository.Setup(item => item.AddAsync(
                It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((item, _) => sentEvent = item)
            .Returns(Task.CompletedTask);
        var sender = new Mock<IUserEmailSender>();
        sender.Setup(item => item.SendAsync(delivery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                message.Status.Should().Be(OutBoxMessageStatus.Processed);
                sentEvent.Should().NotBeNull();
            })
            .Returns(Task.CompletedTask);
        var processor = new UserEmailOutboxProcessor(
            repository.Object, sender.Object, unitOfWork.Object);

        var result = await processor.ProcessAsync(message.Id);

        result.IsSuccess.Should().BeTrue();
        message.Status.Should().Be(OutBoxMessageStatus.Processed);
        message.Payload.Should().Be("{}");
        sentEvent.Should().NotBeNull();
        sentEvent!.Type.Should().Be(OutBoxMessageType.EmailSent);
        var payload = JsonSerializer.Deserialize<EmailSentIntegrationEventPayload>(sentEvent.Payload);
        payload.Should().NotBeNull();
        payload!.DeliveryId.Should().Be(message.Id);
        payload.Category.Should().Be(expectedCategory);
        sentEvent.Payload.Should().NotContain(delivery.Recipient);
        sentEvent.Payload.Should().NotContain(delivery.FirstName);
        sentEvent.Payload.Should().NotContain(delivery.Token);
        unitOfWork.Verify(unit => unit.CommitTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenEmailFails_LeavesMessagePending()
    {
        var delivery = new UserEmailDelivery(
            "jane@example.com", "Jane", "secret-token", UserEmailDeliveryType.PasswordReset);
        var message = new OutboxMessage(
            OutBoxMessageType.PasswordResetRequested,
            JsonSerializer.Serialize(delivery));
        var repository = new Mock<IOutboxMessageRepository>();
        repository.Setup(item => item.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var sender = new Mock<IUserEmailSender>();
        sender.Setup(item => item.SendAsync(delivery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("SMTP unavailable."));
        var unitOfWork = new Mock<IUnitOfWork>();
        var processor = new UserEmailOutboxProcessor(
            repository.Object, sender.Object, unitOfWork.Object);

        var result = await processor.ProcessAsync(message.Id);

        result.IsFailure.Should().BeTrue();
        message.Status.Should().Be(OutBoxMessageStatus.Pending);
        repository.Verify(item => item.AddAsync(
            It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(unit => unit.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenDeliveryWasAlreadyProcessed_DoesNotSendOrPublishAgain()
    {
        var message = new OutboxMessage(
            OutBoxMessageType.EmailConfirmationRequested,
            "{}");
        message.MarkProcessed(clearPayload: true);
        var repository = new Mock<IOutboxMessageRepository>();
        repository.Setup(item => item.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var sender = new Mock<IUserEmailSender>(MockBehavior.Strict);
        var unitOfWork = new Mock<IUnitOfWork>();
        var processor = new UserEmailOutboxProcessor(
            repository.Object, sender.Object, unitOfWork.Object);

        var result = await processor.ProcessAsync(message.Id);

        result.IsSuccess.Should().BeTrue();
        sender.VerifyNoOtherCalls();
        repository.Verify(item => item.AddAsync(
            It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(unit => unit.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
