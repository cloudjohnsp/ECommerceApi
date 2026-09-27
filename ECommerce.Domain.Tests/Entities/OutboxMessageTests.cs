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
