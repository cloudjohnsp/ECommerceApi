using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ECommerce.Application.Abstractions.Observability;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class OutboxMessageRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsMessage()
    {
        await using var context = CreateContext();
        var repository = new OutboxMessageRepository(context);
        var message = new OutboxMessage(OutBoxMessageType.OrderCreated, "{\"orderId\":\"123\"}");

        await repository.AddAsync(message);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.OutboxMessages.SingleAsync();
        persisted.Id.Should().Be(message.Id);
        persisted.Type.Should().Be(OutBoxMessageType.OrderCreated);
        persisted.Status.Should().Be(OutBoxMessageStatus.Pending);
        persisted.Payload.Should().Be(message.Payload);
    }

    [Fact]
    public async Task SaveChanges_AssignsRequestCorrelationToNewOutboxMessage()
    {
        await using var context = CreateContext(new StubCorrelationContext("checkout-123"));
        var message = new OutboxMessage(OutBoxMessageType.OrderCreated, "{}");
        context.OutboxMessages.Add(message);

        await context.SaveChangesAsync();

        message.CorrelationId.Should().Be("checkout-123");
    }

    [Fact]
    public async Task GetByIdAndUpdate_PersistsProcessedPaymentIntention()
    {
        await using var context = CreateContext();
        var repository = new OutboxMessageRepository(context);
        var paymentId = Guid.NewGuid();
        var message = new OutboxMessage(paymentId, OutBoxMessageType.PaymentCreationRequested, "{}");
        await repository.AddAsync(message);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await repository.GetByIdAsync(paymentId);
        persisted.Should().NotBeNull();
        persisted!.MarkProcessed();
        repository.Update(persisted);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var processed = await repository.GetByIdAsync(paymentId);
        processed!.Status.Should().Be(OutBoxMessageStatus.Processed);
        processed.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_WithInMemoryProvider_ReturnsTrackedMessage()
    {
        await using var context = CreateContext();
        var repository = new OutboxMessageRepository(context);
        var message = new OutboxMessage(OutBoxMessageType.PaymentCreationRequested, "{}");
        await repository.AddAsync(message);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await repository.GetByIdForUpdateAsync(message.Id);

        persisted.Should().NotBeNull();
        context.Entry(persisted!).State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public async Task GetPendingIdsAsync_ReturnsRequestedTypesInGlobalCreationOrderWithinBatchSize()
    {
        await using var context = CreateContext();
        var repository = new OutboxMessageRepository(context);
        var first = new OutboxMessage(Guid.NewGuid(), OutBoxMessageType.PaymentCreationRequested, "{}");
        var second = new OutboxMessage(Guid.NewGuid(), OutBoxMessageType.PaymentCreationRequested, "{}");
        var processed = new OutboxMessage(Guid.NewGuid(), OutBoxMessageType.PaymentCreationRequested, "{}");
        processed.MarkProcessed();
        var orderMessage = new OutboxMessage(OutBoxMessageType.OrderCreated, "{}");
        context.OutboxMessages.AddRange(first, second, processed, orderMessage);
        await context.SaveChangesAsync();

        var ids = await repository.GetPendingIdsAsync(
            [OutBoxMessageType.PaymentCreationRequested, OutBoxMessageType.OrderCreated],
            3);

        ids.Should().Equal(first.Id, second.Id, orderMessage.Id);
    }

    private static AppDbContext CreateContext(ICorrelationContext? correlationContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, correlationContext);
    }

    private sealed record StubCorrelationContext(string? CorrelationId) : ICorrelationContext;
}
