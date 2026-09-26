using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Specifications;
using ECommerce.Domain.Enums;

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
    public async Task GetByCustomerIdAsync_ReturnsOnlyCustomersOrders()
    {
        await using var context = CreateContext();
        var customerId = Guid.NewGuid();
        var expected = CreateOrder(customerId);
        context.Orders.AddRange(expected, CreateOrder(Guid.NewGuid()));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new OrderRepository(context);

        var result = await repository.GetByCustomerIdAsync(customerId);

        result.Should().ContainSingle().Which.Id.Should().Be(expected.Id);
        result.Single().Items.Should().ContainSingle();
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

    [Fact]
    public async Task SearchAsync_FiltersSortsAndPaginatesOrders()
    {
        await using var context = CreateContext();
        var customerId = Guid.NewGuid();
        var lowerTotal = CreateOrder(customerId);
        var higherTotal = Order.Create(customerId).Value!;
        higherTotal.AddItem(Guid.NewGuid(), "Workstation", 500m, 2);
        lowerTotal.MarkAsPaid();
        higherTotal.MarkAsPaid();
        var anotherCustomer = CreateOrder(Guid.NewGuid());
        anotherCustomer.MarkAsPaid();
        context.Orders.AddRange(lowerTotal, higherTotal, anotherCustomer);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new OrderRepository(context);
        var specification = new OrderSearchSpecification(new SearchOrdersQuery(
            CustomerId: customerId,
            Status: OrderStatus.Paid,
            SortBy: "total",
            Descending: true,
            Page: 1,
            PageSize: 1));

        var result = await repository.SearchAsync(specification);

        result.TotalCount.Should().Be(2);
        result.Items.Should().ContainSingle().Which.Id.Should().Be(higherTotal.Id);
        result.Items.Single().Items.Should().ContainSingle();
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }

    private static Order CreateOrder(Guid? customerId = null)
    {
        var result = Order.Create(customerId ?? Guid.NewGuid());
        result.Value!.AddItem(Guid.NewGuid(), "Notebook", 100m, 2);
        return result.Value;
    }
}
