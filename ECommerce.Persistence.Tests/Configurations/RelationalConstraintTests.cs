using ECommerce.Domain.Entities;
using ECommerce.Persistence.Contexts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ECommerce.Persistence.Tests.Configurations;

public sealed class RelationalConstraintTests
{
    [Theory]
    [InlineData(typeof(Inventory), "ck_inventories_stock", "stock >= 0")]
    [InlineData(typeof(Inventory), "ck_inventories_reserved_stock", "reserved_stock >= 0")]
    [InlineData(typeof(Inventory), "ck_inventories_reservation_capacity", "reserved_stock <= stock")]
    [InlineData(typeof(Product), "ck_products_price", "price > 0")]
    [InlineData(typeof(OrderItem), "ck_order_items_unit_price", "unit_price > 0")]
    [InlineData(typeof(OrderItem), "ck_order_items_quantity", "quantity > 0")]
    [InlineData(typeof(Order), "ck_orders_status", "status IN (1, 2, 3, 4)")]
    [InlineData(typeof(Payment), "ck_payments_amount", "amount > 0")]
    [InlineData(typeof(Payment), "ck_payments_status", "status IN (1, 2, 3, 4)")]
    public void Model_ProtectsPersistedDomainInvariant(
        Type entityType,
        string constraintName,
        string expectedSql)
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var metadata = model.FindEntityType(entityType);

        metadata.Should().NotBeNull();
        metadata!.GetCheckConstraints().Should().ContainSingle(constraint =>
            constraint.Name == constraintName && constraint.Sql == expectedSql);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model;Username=model;Password=model")
            .Options;

        return new AppDbContext(options);
    }
}
