using System.Text.Json;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Email;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Email;

public sealed class UserEmailOutboxProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WhenEmailIsSent_MarksMessageProcessedAndClearsTokenPayload()
    {
        var delivery = new UserEmailDelivery(
            "jane@example.com", "Jane", "secret-token", UserEmailDeliveryType.EmailConfirmation);
        var message = new OutboxMessage(
            OutBoxMessageType.EmailConfirmationRequested,
            JsonSerializer.Serialize(delivery));
        var repository = new Mock<IOutboxMessageRepository>();
        repository.Setup(item => item.GetByIdAsync(message.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var sender = new Mock<IUserEmailSender>();
        sender.Setup(item => item.SendAsync(delivery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var unitOfWork = new Mock<IUnitOfWork>();
        var processor = new UserEmailOutboxProcessor(
            repository.Object, sender.Object, unitOfWork.Object);

        var result = await processor.ProcessAsync(message.Id);

        result.IsSuccess.Should().BeTrue();
        message.Status.Should().Be(OutBoxMessageStatus.Processed);
        message.Payload.Should().Be("{}");
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
        unitOfWork.Verify(unit => unit.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
