using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class OrderRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsOrderAndItems()
    {
        await using var context = CreateContext();
        var repository = new OrderRepository(context);
        var order = CreateOrder();

        await repository.AddAsync(order);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.Orders.Include(x => x.Items).SingleAsync();
        persisted.Items.Should().ContainSingle();
        persisted.Total.Should().Be(200m);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesItems()
    {
        await using var context = CreateContext();
        var order = CreateOrder();
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new OrderRepository(context);

        var result = await repository.GetByIdAsync(order.Id);

        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsNewestOrderFirst()
    {
        await using var context = CreateContext();
        var first = CreateOrder();
        await Task.Delay(10);
        var second = CreateOrder();
        context.Orders.AddRange(first, second);
        await context.SaveChangesAsync();
        var repository = new OrderRepository(context);

        var result = await repository.GetAllAsync();

        result.Select(x => x.Id).Should().ContainInOrder(second.Id, first.Id);
    }

    [Fact]
    public async Task Update_PersistsStatusChange()
    {
        await using var context = CreateContext();
        var order = CreateOrder();
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        order.MarkAsPaid();
        var repository = new OrderRepository(context);

        repository.Update(order);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        (await context.Orders.SingleAsync()).Status.Should().Be(Domain.Enums.OrderStatus.Paid);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }

    private static Order CreateOrder()
    {
        var result = Order.Create(Guid.NewGuid());
        result.Value!.AddItem(Guid.NewGuid(), "Notebook", 100m, 2);
        return result.Value;
    }
}
