using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class PaymentRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_ReturnsPersistedPayment()
    {
        await using var context = CreateContext();
        var repository = new PaymentRepository(context);
        var payment = Payment.Create(Guid.NewGuid(), 125.50m, "ECommercePayment").Value!;
        await repository.AddAsync(payment);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await repository.GetByIdAsync(payment.Id);

        persisted.Should().NotBeNull();
        persisted!.OrderId.Should().Be(payment.OrderId);
        persisted.Amount.Should().Be(125.50m);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
