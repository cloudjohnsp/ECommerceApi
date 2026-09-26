using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth;
using ECommerce.Application.Auth.Handlers;
using ECommerce.Application.Email;
using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Handlers;
using ECommerce.Application.Orders.Specifications;
using ECommerce.Application.Products.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Repositories;
using ECommerce.Persistence.Options;
using ECommerce.Persistence.Seeding;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerce.Shared.Messaging;
using ECommerce.Shared.Results;

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
            "user_audit_entries",
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
    public async Task UserEmailDelivery_PersistsSanitizedEmailSentEventAtomically()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var delivery = new UserEmailDelivery(
            $"email-{Guid.NewGuid():N}@example.com",
            "Integration",
            $"token-{Guid.NewGuid():N}",
            UserEmailDeliveryType.EmailConfirmation);
        var sourceMessage = new OutboxMessage(
            OutBoxMessageType.EmailConfirmationRequested,
            JsonSerializer.Serialize(delivery));

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.OutboxMessages.AddAsync(sourceMessage);
            await seedContext.SaveChangesAsync();
        }

        await using (var commandContext = new AppDbContext(options))
        {
            var processor = new UserEmailOutboxProcessor(
                new OutboxMessageRepository(commandContext),
                new SuccessfulUserEmailSender(),
                new UnitOfWork(commandContext));

            var result = await processor.ProcessAsync(sourceMessage.Id);

            result.IsSuccess.Should().BeTrue(string.Join("; ", result.Errors));
        }

        await using var assertionContext = new AppDbContext(options);
        var persistedSource = await assertionContext.OutboxMessages
            .SingleAsync(message => message.Id == sourceMessage.Id);
        var sentEvent = await assertionContext.OutboxMessages.SingleAsync(message =>
            message.Type == OutBoxMessageType.EmailSent &&
            message.Payload.Contains(sourceMessage.Id.ToString()));
        var payload = JsonSerializer.Deserialize<EmailSentIntegrationEventPayload>(sentEvent.Payload);

        persistedSource.Status.Should().Be(OutBoxMessageStatus.Processed);
        persistedSource.Payload.Should().Be("{}");
        sentEvent.Status.Should().Be(OutBoxMessageStatus.Pending);
        payload!.DeliveryId.Should().Be(sourceMessage.Id);
        payload.Category.Should().Be(EmailDeliveryCategories.EmailConfirmation);
        sentEvent.Payload.Should().NotContain(delivery.Recipient);
        sentEvent.Payload.Should().NotContain(delivery.Token);
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
        var stockMessage = await assertionContext.OutboxMessages
            .SingleAsync(message => message.Type == OutBoxMessageType.StockUpdated);

        persistedProduct.AvailableStock.Should().Be(7);
        persistedOrder.Items.Should().ContainSingle(item =>
            item.ProductId == product.Id && item.Quantity == 3);
        outboxMessage.Status.Should().Be(OutBoxMessageStatus.Pending);
        stockMessage.Status.Should().Be(OutBoxMessageStatus.Pending);
        stockMessage.Payload.Should().Contain(product.Id.ToString());
        stockMessage.Payload.Should().Contain("reserved");
    }

    [PostgreSqlIntegrationFact]
    public async Task AddOrderItem_PersistsReservationOrderAndUpdatedEventAtomically()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var customer = User.Create(
            "Order",
            "Customer",
            Email.Create($"order-items-{suffix}@example.com").Value!,
            "password-hash").Value!;
        var firstProduct = Product.Create($"First {suffix}", "First product", 49.90m, 10).Value!;
        var secondProduct = Product.Create($"Second {suffix}", "Second product", 25m, 8).Value!;

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Users.AddAsync(customer);
            await seedContext.Products.AddRangeAsync(firstProduct, secondProduct);
            await seedContext.SaveChangesAsync();
        }

        Guid orderId;
        await using (var createContext = new AppDbContext(options))
        {
            var createHandler = new CreateOrderHandler(
                new OrderRepository(createContext),
                new UserRepository(createContext),
                new ProductRepository(createContext),
                new OutboxMessageRepository(createContext),
                new UnitOfWork(createContext),
                new NoOpProductCache());
            var createResult = await createHandler.Handle(
                new CreateOrderCommand(customer.Id, [new CreateOrderItem(firstProduct.Id, 1)]),
                CancellationToken.None);
            createResult.IsSuccess.Should().BeTrue(string.Join("; ", createResult.Errors));
            orderId = createResult.Value!.Id;
        }

        await using (var addContext = new AppDbContext(options))
        {
            var addHandler = new AddOrderItemHandler(
                new OrderRepository(addContext),
                new ProductRepository(addContext),
                new OutboxMessageRepository(addContext),
                new UnitOfWork(addContext),
                new NoOpProductCache());
            var addResult = await addHandler.Handle(
                new AddOrderItemCommand(orderId, secondProduct.Id, 2, customer.Id),
                CancellationToken.None);
            addResult.IsSuccess.Should().BeTrue(string.Join("; ", addResult.Errors));
        }

        await using var assertionContext = new AppDbContext(options);
        var order = await assertionContext.Orders
            .Include(item => item.Items)
            .SingleAsync(item => item.Id == orderId);
        var product = await assertionContext.Products
            .Include(item => item.Inventory)
            .SingleAsync(item => item.Id == secondProduct.Id);
        var eventTypes = await assertionContext.OutboxMessages
            .Where(message => message.Payload.Contains(orderId.ToString()))
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Type)
            .ToArrayAsync();

        order.Items.Should().HaveCount(2);
        product.AvailableStock.Should().Be(6);
        eventTypes.Where(type => type != OutBoxMessageType.StockUpdated)
            .Should().Equal(OutBoxMessageType.OrderCreated, OutBoxMessageType.OrderUpdated);
        eventTypes.Count(type => type == OutBoxMessageType.StockUpdated).Should().Be(2);
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

    [PostgreSqlIntegrationFact]
    public async Task UserAuditEntry_PersistsJsonbAndActorRelationship()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var administrator = User.Create(
            "Audit",
            "Administrator",
            Email.Create($"audit-admin-{suffix}@example.com").Value!,
            "hash",
            UserRole.Administrator).Value!;
        var customer = User.Create(
            "Audit",
            "Customer",
            Email.Create($"audit-customer-{suffix}@example.com").Value!,
            "hash").Value!;

        await using (var writeContext = new AppDbContext(options))
        {
            writeContext.Users.AddRange(administrator, customer);
            writeContext.UserAuditEntries.Add(new UserAuditEntry(
                customer.Id,
                administrator.Id,
                UserAuditAction.RoleChanged,
                "{\"role\":{\"from\":\"Customer\",\"to\":\"Administrator\"}}"));
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new AppDbContext(options);
        var history = await new UserAuditRepository(readContext)
            .GetByUserIdAsync(customer.Id, 1, 20);

        history.Items.Should().ContainSingle();
        history.Items.Single().ActorUserId.Should().Be(administrator.Id);
        history.Items.Single().ChangesJson.Should().Contain("Administrator");
    }

    [PostgreSqlIntegrationFact]
    public async Task SalesReport_AggregationsAreTranslatedByPostgreSql()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var customer = User.Create(
            "Report",
            "Customer",
            Email.Create($"report-{suffix}@example.com").Value!,
            "hash").Value!;
        var product = Product.Create($"Report product {suffix}", "", 3.25m, 200).Value!;
        var order = Order.Create(customer.Id).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 123);
        order.MarkAsPaid();
        var payment = Payment.Create(order.Id, order.Total, "integration").Value!;
        payment.MarkAsPaid($"pay_{suffix}");

        await using (var writeContext = new AppDbContext(options))
        {
            await writeContext.AddRangeAsync(customer, product, order, payment);
            await writeContext.SaveChangesAsync();
        }

        await using var reportContext = new AppDbContext(options);
        var report = await new AdminReportingRepository(reportContext).GetSalesReportAsync(
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddHours(1),
            50);

        report.TopProducts.Should().Contain(item =>
            item.ProductId == product.Id &&
            item.Quantity == 123 &&
            item.GrossRevenue == 399.75m);
        report.DailySales.Should().Contain(item => item.SuccessfulPayments > 0);

        var orderSearch = await new OrderRepository(reportContext).SearchAsync(
            new OrderSearchSpecification(new SearchOrdersQuery(
                CustomerId: customer.Id,
                Status: OrderStatus.Paid,
                SortBy: "total",
                Descending: true)));
        orderSearch.Items.Should().ContainSingle().Which.Id.Should().Be(order.Id);
    }

    [PostgreSqlIntegrationFact]
    public async Task RefreshTokenRotation_ConcurrentReuseProducesOneChainAndRevokesIt()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        const string rawRefreshToken = "concurrent-original-token";
        var tokenService = new TestJwtTokenService();
        var user = User.Create(
            "Concurrent",
            "Customer",
            Email.Create($"refresh-{Guid.NewGuid():N}@example.com").Value!,
            "hash").Value!;
        user.ConfirmEmail();
        var originalToken = RefreshToken.Create(
            user.Id,
            tokenService.HashRefreshToken(rawRefreshToken),
            DateTimeOffset.UtcNow.AddDays(1));

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.AddRangeAsync(user, originalToken);
            await seedContext.SaveChangesAsync();
        }

        async Task<ECommerce.Shared.Results.Result<ECommerce.Application.Auth.Dtos.AuthTokensDto>> RotateAsync()
        {
            await using var context = new AppDbContext(options);
            var handler = new RefreshTokenHandler(
                new RefreshTokenRepository(context),
                new UserRepository(context),
                new UnitOfWork(context),
                tokenService);
            return await handler.Handle(
                new RefreshTokenCommand(rawRefreshToken),
                CancellationToken.None);
        }

        var results = await Task.WhenAll(RotateAsync(), RotateAsync());

        results.Should().ContainSingle(result => result.IsSuccess);
        results.Should().ContainSingle(result => result.IsFailure);
        await using var assertionContext = new AppDbContext(options);
        var persistedTokens = await assertionContext.RefreshTokens
            .Where(token => token.UserId == user.Id)
            .ToArrayAsync();
        persistedTokens.Should().HaveCount(2);
        persistedTokens.Should().OnlyContain(token => token.RevokedAt != null);
    }

    [PostgreSqlIntegrationFact]
    public async Task Logout_ConcurrentWithRotation_LeavesNoActiveRefreshTokens()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        const string rawRefreshToken = "concurrent-logout-token";
        var tokenService = new TestJwtTokenService();
        var user = User.Create(
            "Logout",
            "Customer",
            Email.Create($"logout-{Guid.NewGuid():N}@example.com").Value!,
            "hash").Value!;
        user.ConfirmEmail();
        var originalToken = RefreshToken.Create(
            user.Id,
            tokenService.HashRefreshToken(rawRefreshToken),
            DateTimeOffset.UtcNow.AddDays(1));

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.AddRangeAsync(user, originalToken);
            await seedContext.SaveChangesAsync();
        }

        async Task RotateAsync()
        {
            await using var context = new AppDbContext(options);
            var handler = new RefreshTokenHandler(
                new RefreshTokenRepository(context),
                new UserRepository(context),
                new UnitOfWork(context),
                tokenService);
            await handler.Handle(new RefreshTokenCommand(rawRefreshToken), CancellationToken.None);
        }

        async Task LogoutAsync()
        {
            await using var context = new AppDbContext(options);
            var handler = new LogoutHandler(
                new RefreshTokenRepository(context),
                new UnitOfWork(context),
                tokenService);
            var result = await handler.Handle(
                new LogoutCommand(rawRefreshToken),
                CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
        }

        await Task.WhenAll(RotateAsync(), LogoutAsync());

        await using var assertionContext = new AppDbContext(options);
        var persistedTokens = await assertionContext.RefreshTokens
            .Where(token => token.UserId == user.Id)
            .ToArrayAsync();
        persistedTokens.Should().NotBeEmpty();
        persistedTokens.Should().OnlyContain(token => token.RevokedAt != null);
    }

    [PostgreSqlIntegrationFact]
    public async Task ResetPassword_ConcurrentUse_ConsumesTokenExactlyOnce()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        const string rawToken = "concurrent-password-reset-token";
        var tokenService = new TestUserActionTokenService();
        var user = User.Create(
            "Reset",
            "Customer",
            Email.Create($"reset-{Guid.NewGuid():N}@example.com").Value!,
            "old-hash").Value!;
        var token = UserActionToken.Create(
            user.Id,
            tokenService.Hash(rawToken),
            UserActionTokenType.PasswordReset,
            DateTimeOffset.UtcNow.AddHours(1)).Value!;

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.AddRangeAsync(user, token);
            await seedContext.SaveChangesAsync();
        }

        async Task<ECommerce.Shared.Results.Result> ResetAsync()
        {
            await using var context = new AppDbContext(options);
            var handler = new ResetPasswordHandler(
                new UserActionTokenRepository(context),
                new UserRepository(context),
                new RefreshTokenRepository(context),
                tokenService,
                new TestPasswordHasher(),
                new UnitOfWork(context),
                new UserAuditRepository(context));
            return await handler.Handle(
                new ResetPasswordCommand(rawToken, "NewPassword1!"),
                CancellationToken.None);
        }

        var results = await Task.WhenAll(ResetAsync(), ResetAsync());

        results.Should().ContainSingle(result => result.IsSuccess);
        results.Should().ContainSingle(result => result.IsFailure);
        await using var assertionContext = new AppDbContext(options);
        (await assertionContext.UserActionTokens.SingleAsync(item => item.Id == token.Id))
            .ConsumedAt.Should().NotBeNull();
        (await assertionContext.UserAuditEntries.CountAsync(entry =>
            entry.UserId == user.Id && entry.Action == UserAuditAction.PasswordChanged)).Should().Be(1);
    }

    [PostgreSqlIntegrationFact]
    public async Task ForgotPassword_ConcurrentRequests_LeaveOneUsableToken()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var tokenService = new TestUserActionTokenService();
        var user = User.Create(
            "Forgot",
            "Customer",
            Email.Create($"forgot-{Guid.NewGuid():N}@example.com").Value!,
            "hash").Value!;
        user.ConfirmEmail();

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Users.AddAsync(user);
            await seedContext.SaveChangesAsync();
        }

        async Task RequestResetAsync()
        {
            await using var context = new AppDbContext(options);
            var handler = new ForgotPasswordHandler(
                new UserRepository(context),
                new UserActionTokenRepository(context),
                new OutboxMessageRepository(context),
                tokenService,
                new UnitOfWork(context));
            var result = await handler.Handle(
                new ForgotPasswordCommand(user.Email.Value),
                CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
        }

        await Task.WhenAll(RequestResetAsync(), RequestResetAsync());

        await using var assertionContext = new AppDbContext(options);
        var now = DateTimeOffset.UtcNow;
        var usableTokens = await assertionContext.UserActionTokens.CountAsync(token =>
            token.UserId == user.Id &&
            token.Type == UserActionTokenType.PasswordReset &&
            token.ConsumedAt == null &&
            token.ExpiresAt > now);
        usableTokens.Should().Be(1);
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

    private sealed class SuccessfulUserEmailSender : IUserEmailSender
    {
        public Task<Result> SendAsync(
            UserEmailDelivery delivery,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }

    private sealed class TestJwtTokenService : IJwtTokenService
    {
        private int _sequence;

        public int AccessTokenExpiresInSeconds => 900;

        public string GenerateAccessToken(User user) => $"access-{user.Id}";

        public string GenerateRefreshToken() => $"rotated-{Interlocked.Increment(ref _sequence)}";

        public string HashRefreshToken(string refreshToken) => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

        public DateTimeOffset GetRefreshTokenExpiresAt() => DateTimeOffset.UtcNow.AddDays(7);
    }

    private sealed class TestUserActionTokenService : IUserActionTokenService
    {
        private int _sequence;

        public IssuedUserActionToken Issue()
        {
            var rawToken = $"action-{Interlocked.Increment(ref _sequence)}-{Guid.NewGuid():N}";
            return new IssuedUserActionToken(rawToken, Hash(rawToken));
        }

        public string Hash(string rawToken) => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
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
