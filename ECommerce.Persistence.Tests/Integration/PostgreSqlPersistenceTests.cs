using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Handlers;
using ECommerce.Application.Products.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using ECommerce.Persistence.Options;
using ECommerce.Persistence.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECommerce.Persistence.Tests.Integration;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class PostgreSqlPersistenceTests(PostgreSqlContainerFixture fixture)
{
    [PostgreSqlIntegrationFact]
    public async Task Migrations_CreateExpectedSchema()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public';
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        var tables = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        tables.Should().Contain(
        [
            "users",
            "refresh_tokens",
            "user_action_tokens",
            "products",
            "product_images",
            "categories",
            "inventories",
            "orders",
            "order_items",
            "payments",
            "OutboxMessages"
        ]);
    }

    [PostgreSqlIntegrationFact]
    public async Task CreateOrder_PersistsReservationOrderAndOutboxAtomically()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var email = Email.Create($"integration-{Guid.NewGuid():N}@example.com").Value!;
        var customer = User.Create("Integration", "Customer", email, "password-hash").Value!;
        var product = Product.Create("Integration product", "PostgreSQL test", 49.90m, 10).Value!;

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Users.AddAsync(customer);
            await seedContext.Products.AddAsync(product);
            await seedContext.SaveChangesAsync();
        }

        await using (var commandContext = new AppDbContext(options))
        {
            var handler = new CreateOrderHandler(
                new OrderRepository(commandContext),
                new UserRepository(commandContext),
                new ProductRepository(commandContext),
                new OutboxMessageRepository(commandContext),
                new UnitOfWork(commandContext),
                new NoOpProductCache());

            var result = await handler.Handle(
                new CreateOrderCommand(customer.Id, [new CreateOrderItem(product.Id, 3)]),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue(string.Join("; ", result.Errors));
        }

        await using var assertionContext = new AppDbContext(options);
        var persistedProduct = await assertionContext.Products
            .Include(item => item.Inventory)
            .SingleAsync(item => item.Id == product.Id);
        var persistedOrder = await assertionContext.Orders
            .Include(item => item.Items)
            .SingleAsync(item => item.CustomerId == customer.Id);
        var outboxMessage = await assertionContext.OutboxMessages
            .SingleAsync(message => message.Type == OutBoxMessageType.OrderCreated);

        persistedProduct.AvailableStock.Should().Be(7);
        persistedOrder.Items.Should().ContainSingle(item =>
            item.ProductId == product.Id && item.Quantity == 3);
        outboxMessage.Status.Should().Be(OutBoxMessageStatus.Pending);
    }

    [PostgreSqlIntegrationFact]
    public async Task SaveChanges_DuplicateEmail_IsRejectedByPostgreSqlUniqueIndex()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        await using var context = new AppDbContext(CreateOptions(connectionString));
        var address = $"duplicate-{Guid.NewGuid():N}@example.com";
        var first = User.Create("First", "Customer", Email.Create(address).Value!, "hash").Value!;
        var second = User.Create("Second", "Customer", Email.Create(address).Value!, "hash").Value!;

        await context.Users.AddRangeAsync(first, second);

        var action = () => context.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [PostgreSqlIntegrationFact]
    public async Task DatabaseSeeder_IsIdempotentOnPostgreSql()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var seedOptions = new DatabaseSeedOptions
        {
            Enabled = true,
            SeedSampleCatalog = true,
            AdministratorEmail = $"seed-{Guid.NewGuid():N}@example.com",
            AdministratorPassword = "StrongPassword1!"
        };

        await using (var firstContext = new AppDbContext(CreateOptions(connectionString)))
            await CreateSeeder(firstContext, seedOptions).SeedAsync();
        await using (var secondContext = new AppDbContext(CreateOptions(connectionString)))
            await CreateSeeder(secondContext, seedOptions).SeedAsync();

        await using var assertionContext = new AppDbContext(CreateOptions(connectionString));
        (await assertionContext.Users.CountAsync(user =>
            user.Email.Value == seedOptions.AdministratorEmail)).Should().Be(1);
        (await assertionContext.Categories.CountAsync(category =>
            category.Slug == "electronics")).Should().Be(1);
        (await assertionContext.Products.CountAsync(product =>
            product.Name == "Wireless Mouse")).Should().Be(1);
    }

    private static DbContextOptions<AppDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

    private static DatabaseSeeder CreateSeeder(AppDbContext context, DatabaseSeedOptions options) =>
        new(context, new TestPasswordHasher(), Microsoft.Extensions.Options.Options.Create(options));

    private sealed class TestPasswordHasher : ECommerce.Application.Abstractions.Security.IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";

        public bool VerifyPassword(string password, string hashedPassword) =>
            hashedPassword == HashPassword(password);
    }

    private sealed class NoOpProductCache : IProductCache
    {
        public Task<ProductDto?> GetAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDto?>(null);

        public Task SetAsync(ProductDto product, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
