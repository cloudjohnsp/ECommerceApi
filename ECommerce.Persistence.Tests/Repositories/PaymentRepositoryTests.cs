using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ECommerce.Persistence.Tests.Repositories;

public sealed class PaymentRepositoryTests
{
    [Fact]
    public void Model_RequiresValidatedThreeLetterCurrency()
    {
        using var context = CreateRelationalModelContext();
        var entityType = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Payment));

        entityType.Should().NotBeNull();
        var currency = entityType!.FindProperty(nameof(Payment.Currency));
        currency.Should().NotBeNull();
        currency!.IsNullable.Should().BeFalse();
        currency.GetColumnType().Should().Be("character(3)");
        entityType.GetCheckConstraints().Should().ContainSingle(constraint =>
            constraint.Name == "ck_payments_currency" &&
            constraint.Sql == "\"currency\" ~ '^[A-Z]{3}$'");
    }

    [Fact]
    public void Model_EnforcesAttemptIdempotencyAndSinglePendingOrPaidPayment()
    {
        using var context = CreateRelationalModelContext();
        var entityType = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Payment));

        entityType.Should().NotBeNull();
        entityType!.FindProperty(nameof(Payment.IdempotencyKey))!.IsNullable.Should().BeFalse();
        entityType.GetIndexes().Should().Contain(index =>
            index.GetDatabaseName() == "ux_payments_order_id_idempotency_key" &&
            index.IsUnique);
        entityType.GetIndexes().Should().Contain(index =>
            index.GetDatabaseName() == "ux_payments_one_pending_per_order" &&
            index.IsUnique &&
            index.GetFilter() == "status = 1");
        entityType.GetIndexes().Should().Contain(index =>
            index.GetDatabaseName() == "ux_payments_one_paid_per_order" &&
            index.IsUnique &&
            index.GetFilter() == "status = 2");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsPersistedPayment()
    {
        await using var context = CreateContext();
        var repository = new PaymentRepository(context);
        var payment = Payment.Create(Guid.NewGuid(), 125.50m, "BRL", "ECommercePayment").Value!;
        await repository.AddAsync(payment);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await repository.GetByIdAsync(payment.Id);

        persisted.Should().NotBeNull();
        persisted!.OrderId.Should().Be(payment.OrderId);
        persisted.Amount.Should().Be(125.50m);
        persisted.Currency.Should().Be("BRL");
        context.Entry(persisted).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_WithInMemoryProvider_ReturnsTrackedPayment()
    {
        await using var context = CreateContext();
        var repository = new PaymentRepository(context);
        var payment = Payment.Create(Guid.NewGuid(), 125.50m, "BRL", "ECommercePayment").Value!;
        await repository.AddAsync(payment);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await repository.GetByIdForUpdateAsync(payment.Id);

        persisted.Should().NotBeNull();
        context.Entry(persisted!).State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public async Task GetByOrderIdAsync_ReturnsDetachedPayment()
    {
        await using var context = CreateContext();
        var repository = new PaymentRepository(context);
        var payment = Payment.Create(Guid.NewGuid(), 125.50m, "BRL", "ECommercePayment").Value!;
        await repository.AddAsync(payment);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await repository.GetByOrderIdAsync(payment.OrderId);

        persisted.Should().ContainSingle();
        context.Entry(persisted.Single()).State.Should().Be(EntityState.Detached);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static AppDbContext CreateRelationalModelContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model;Username=model;Password=model")
            .Options;

        return new AppDbContext(options);
    }
}
