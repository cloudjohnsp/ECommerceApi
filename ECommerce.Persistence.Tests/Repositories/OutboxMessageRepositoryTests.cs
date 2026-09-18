using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

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

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
