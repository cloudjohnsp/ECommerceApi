using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public sealed class OutboxMessageTests
{
    [Fact]
    public void Constructor_InitializesDurableDispatchState()
    {
        var message = new OutboxMessage(OutBoxMessageType.OrderCreated, "{}");

        message.Status.Should().Be(OutBoxMessageStatus.Pending);
        message.NextAttemptAt.Should().Be(message.CreatedAt);
        message.Attempts.Should().Be(0);
        message.ProcessedAt.Should().BeNull();
        message.LockId.Should().BeNull();
        message.LockedUntil.Should().BeNull();
        message.LastError.Should().BeNull();
        message.CorrelationId.Should().Be(message.Id.ToString("N"));
    }

    [Fact]
    public void Constructor_NormalizesExplicitCorrelationId()
    {
        var message = new OutboxMessage(
            OutBoxMessageType.OrderCreated,
            "{}",
            " checkout-123 ");

        message.CorrelationId.Should().Be("checkout-123");
    }

    [Fact]
    public void MarkProcessed_RecordsCompletionAndClearsDispatchState()
    {
        var message = new OutboxMessage(OutBoxMessageType.OrderCreated, "{}");

        message.MarkProcessed();

        message.Status.Should().Be(OutBoxMessageStatus.Processed);
        message.ProcessedAt.Should().NotBeNull();
        message.UpdatedAt.Should().Be(message.ProcessedAt);
    }
}
