using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Orders;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth;
using ECommerce.Application.Auth.Handlers;
using ECommerce.Application.Email;
using ECommerce.Application.Orders;
using ECommerce.Application.Orders.Handlers;
using ECommerce.Application.Orders.Specifications;
using ECommerce.Application.Payments;
using ECommerce.Application.Payments.Handlers;
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
            "inventory_reservations",
            "orders",
            "order_items",
            "payments",
            "OutboxMessages"
        ]);
    }

    [PostgreSqlIntegrationFact]
    public async Task InventoryConstraints_RejectReservationAbovePhysicalStock()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var product = Product.Create(
            $"Constraint product {Guid.NewGuid():N}",
            "PostgreSQL constraint test",
            10m,
            5).Value!;

        await using (var context = new AppDbContext(options))
        {
            await context.Products.AddAsync(product);
            await context.SaveChangesAsync();
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            UPDATE inventories
            SET reserved_stock = stock + 1
            WHERE product_id = @productId;
            """,
            connection);
        command.Parameters.AddWithValue("productId", product.Id);

        var action = async () => await command.ExecuteNonQueryAsync();

        var exception = await action.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        exception.Which.ConstraintName.Should().Be("ck_inventories_reservation_capacity");
    }

    [PostgreSqlIntegrationFact]
    public async Task ProductForUpdate_DoesNotReturnSoftDeletedProduct()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var product = Product.Create(
            $"Inactive product {Guid.NewGuid():N}",
            "PostgreSQL query-filter test",
            10m,
            1).Value!;
        product.Deactivate().IsSuccess.Should().BeTrue();

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Products.AddAsync(product);
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        await unitOfWork.BeginTransactionAsync();

        var lockedProduct = await new ProductRepository(context)
            .GetByIdForUpdateAsync(product.Id);

        lockedProduct.Should().BeNull();
        await unitOfWork.CommitTransactionAsync();
    }

    [PostgreSqlIntegrationFact]
    public async Task OrderItemConstraint_RejectsDuplicateProductWithinOrder()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var customer = User.Create(
            "Constraint",
            "Customer",
            Email.Create($"order-item-{suffix}@example.com").Value!,
            "hash").Value!;
        var product = Product.Create($"Order item {suffix}", "", 10m, 5).Value!;
        var order = Order.Create(customer.Id).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 1).IsSuccess.Should().BeTrue();

        await using (var context = new AppDbContext(options))
        {
            await context.AddRangeAsync(customer, product, order);
            await context.SaveChangesAsync();
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO order_items
                ("Id", order_id, product_id, product_name, unit_price, quantity)
            VALUES
                (@id, @orderId, @productId, @productName, @unitPrice, @quantity);
            """,
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("orderId", order.Id);
        command.Parameters.AddWithValue("productId", product.Id);
        command.Parameters.AddWithValue("productName", product.Name);
        command.Parameters.AddWithValue("unitPrice", product.Price);
        command.Parameters.AddWithValue("quantity", 1);

        var action = async () => await command.ExecuteNonQueryAsync();

        var exception = await action.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        exception.Which.ConstraintName.Should().Be("ux_order_items_order_id_product_id");
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
                new InventoryReservationRepository(commandContext),
                new OutboxMessageRepository(commandContext),
                new UnitOfWork(commandContext),
                new NoOpProductCache(),
                new FixedOrderExpirationPolicy());

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
        var persistedReservation = await assertionContext.InventoryReservations
            .SingleAsync(item => item.OrderId == persistedOrder.Id);
        var outboxMessage = await assertionContext.OutboxMessages
            .SingleAsync(message => message.Type == OutBoxMessageType.OrderCreated);
        var stockMessage = await assertionContext.OutboxMessages
            .SingleAsync(message => message.Type == OutBoxMessageType.StockUpdated);

        persistedProduct.AvailableStock.Should().Be(7);
        persistedOrder.Items.Should().ContainSingle(item =>
            item.ProductId == product.Id && item.Quantity == 3);
        persistedReservation.ProductId.Should().Be(product.Id);
        persistedReservation.InventoryId.Should().Be(persistedProduct.Inventory.Id);
        persistedReservation.Quantity.Should().Be(3);
        persistedReservation.Status.Should().Be(InventoryReservationStatus.Active);
        persistedReservation.ExpiresAt.Should().BeAfter(persistedReservation.CreatedAt);
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
                new InventoryReservationRepository(createContext),
                new OutboxMessageRepository(createContext),
                new UnitOfWork(createContext),
                new NoOpProductCache(),
                new FixedOrderExpirationPolicy());
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
                new InventoryReservationRepository(addContext),
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
        var reservations = await assertionContext.InventoryReservations
            .Where(item => item.OrderId == orderId)
            .OrderBy(item => item.ProductId)
            .ToArrayAsync();
        var eventTypes = await assertionContext.OutboxMessages
            .Where(message => message.Payload.Contains(orderId.ToString()))
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Type)
            .ToArrayAsync();

        order.Items.Should().HaveCount(2);
        reservations.Should().HaveCount(2);
        reservations.Should().OnlyContain(item => item.Status == InventoryReservationStatus.Active);
        product.AvailableStock.Should().Be(6);
        eventTypes.Where(type => type != OutBoxMessageType.StockUpdated)
            .Should().Equal(OutBoxMessageType.OrderCreated, OutBoxMessageType.OrderUpdated);
        eventTypes.Count(type => type == OutBoxMessageType.StockUpdated).Should().Be(2);
    }

    [PostgreSqlIntegrationFact]
    public async Task CreateOrder_ConcurrentReservations_DoNotOversellInventory()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var firstCustomer = User.Create(
            "First",
            "Customer",
            Email.Create($"first-{suffix}@example.com").Value!,
            "password-hash").Value!;
        var secondCustomer = User.Create(
            "Second",
            "Customer",
            Email.Create($"second-{suffix}@example.com").Value!,
            "password-hash").Value!;
        var product = Product.Create(
            $"Concurrent product {suffix}",
            "Inventory lock test",
            10m,
            5).Value!;

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Users.AddRangeAsync(firstCustomer, secondCustomer);
            await seedContext.Products.AddAsync(product);
            await seedContext.SaveChangesAsync();
        }

        static CreateOrderHandler CreateHandler(AppDbContext context) => new(
            new OrderRepository(context),
            new UserRepository(context),
            new ProductRepository(context),
            new InventoryReservationRepository(context),
            new OutboxMessageRepository(context),
            new UnitOfWork(context),
            new NoOpProductCache(),
            new FixedOrderExpirationPolicy());

        await using var firstContext = new AppDbContext(options);
        await using var secondContext = new AppDbContext(options);
        var firstTask = CreateHandler(firstContext).Handle(
            new CreateOrderCommand(
                firstCustomer.Id,
                [new CreateOrderItem(product.Id, 4)]),
            CancellationToken.None);
        var secondTask = CreateHandler(secondContext).Handle(
            new CreateOrderCommand(
                secondCustomer.Id,
                [new CreateOrderItem(product.Id, 4)]),
            CancellationToken.None);

        var results = await Task.WhenAll(firstTask, secondTask);

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => result.IsFailure).Should().Be(1);
        results.Single(result => result.IsFailure).Errors
            .Should().Contain(error => error.Contains("Insufficient available stock"));

        await using var assertionContext = new AppDbContext(options);
        var persistedProduct = await assertionContext.Products
            .Include(item => item.Inventory)
            .SingleAsync(item => item.Id == product.Id);
        var reservations = await assertionContext.InventoryReservations
            .Where(item => item.ProductId == product.Id)
            .ToArrayAsync();

        persistedProduct.AvailableStock.Should().Be(1);
        reservations.Should().ContainSingle();
        reservations[0].Quantity.Should().Be(4);
        reservations[0].Status.Should().Be(InventoryReservationStatus.Active);
    }

    [PostgreSqlIntegrationFact]
    public async Task CreatePayment_ConcurrentAttemptsAllowOnePendingAndRetryAfterFailure()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var customer = User.Create(
            "Payment",
            "Customer",
            Email.Create($"payment-attempts-{suffix}@example.com").Value!,
            "password-hash").Value!;
        var product = Product.Create(
            $"Payment product {suffix}",
            "Multiple payment attempts test",
            25m,
            10).Value!;
        var order = Order.Create(customer.Id).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 2);

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Users.AddAsync(customer);
            await seedContext.Products.AddAsync(product);
            await seedContext.Orders.AddAsync(order);
            await seedContext.SaveChangesAsync();
        }

        static CreatePaymentHandler CreateHandler(AppDbContext context)
        {
            var payments = new PaymentRepository(context);
            return new CreatePaymentHandler(
                new OrderRepository(context),
                payments,
                new OutboxMessageRepository(context),
                new PersistedPaymentCreationProcessor(payments),
                new UnitOfWork(context));
        }

        await using var firstContext = new AppDbContext(options);
        await using var secondContext = new AppDbContext(options);
        var attempts = await Task.WhenAll(
            CreateHandler(firstContext).Handle(
                new CreatePaymentCommand(order.Id, "BRL", "attempt-1", customer.Id),
                CancellationToken.None),
            CreateHandler(secondContext).Handle(
                new CreatePaymentCommand(order.Id, "BRL", "attempt-2", customer.Id),
                CancellationToken.None));

        attempts.Count(result => result.IsSuccess).Should().Be(1);
        attempts.Count(result => result.IsFailure).Should().Be(1);
        attempts.Single(result => result.IsFailure).Errors.Should()
            .Contain("A payment attempt is already pending for this order.");

        await using (var failureContext = new AppDbContext(options))
        {
            var pending = await failureContext.Payments.SingleAsync(
                payment => payment.OrderId == order.Id);
            pending.MarkAsFailed().IsSuccess.Should().BeTrue();
            await failureContext.SaveChangesAsync();
        }

        await using (var retryContext = new AppDbContext(options))
        {
            var retry = await CreateHandler(retryContext).Handle(
                new CreatePaymentCommand(order.Id, "BRL", "attempt-3", customer.Id),
                CancellationToken.None);
            retry.IsSuccess.Should().BeTrue(string.Join("; ", retry.Errors));
        }

        await using var assertionContext = new AppDbContext(options);
        var persistedAttempts = await assertionContext.Payments
            .Where(payment => payment.OrderId == order.Id)
            .OrderBy(payment => payment.CreatedAt)
            .ToArrayAsync();
        persistedAttempts.Should().HaveCount(2);
        persistedAttempts.Count(payment => payment.Status == PaymentStatus.Failed).Should().Be(1);
        persistedAttempts.Count(payment => payment.Status == PaymentStatus.Pending).Should().Be(1);
        persistedAttempts.Select(payment => payment.IdempotencyKey)
            .Should().OnlyHaveUniqueItems();
    }

    [PostgreSqlIntegrationFact]
    public async Task OrderExpiration_ReleasesReservationAndIsIdempotent()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        var customer = User.Create(
            "Expired",
            "Customer",
            Email.Create($"expired-{suffix}@example.com").Value!,
            "password-hash").Value!;
        var product = Product.Create(
            $"Expired product {suffix}",
            "Order expiration test",
            25m,
            10).Value!;
        product.ReserveStock(3);
        var order = Order.Create(
            customer.Id, now.AddHours(-1), now.AddMinutes(-30)).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 3);
        var reservation = InventoryReservation.Create(
            order.Id, product.Id, product.Inventory.Id, 3,
            order.CreatedAt, order.ExpiresAt).Value!;

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Users.AddAsync(customer);
            await seedContext.Products.AddAsync(product);
            await seedContext.Orders.AddAsync(order);
            await seedContext.InventoryReservations.AddAsync(reservation);
            await seedContext.SaveChangesAsync();
        }

        static OrderExpirationProcessor CreateProcessor(AppDbContext context) => new(
            new OrderRepository(context),
            new InventoryReservationRepository(context),
            new ProductRepository(context),
            new OutboxMessageRepository(context),
            new UnitOfWork(context),
            new NoOpProductCache());

        await using (var firstContext = new AppDbContext(options))
        {
            var result = await CreateProcessor(firstContext).ProcessBatchAsync(10, now);
            result.IsSuccess.Should().BeTrue(string.Join("; ", result.Errors));
            result.Value!.ExpiredCount.Should().Be(1);
        }
        await using (var retryContext = new AppDbContext(options))
        {
            var retry = await CreateProcessor(retryContext).ProcessBatchAsync(10, now);
            retry.IsSuccess.Should().BeTrue();
            retry.Value!.CandidateCount.Should().Be(0);
        }

        await using var assertionContext = new AppDbContext(options);
        var persistedOrder = await assertionContext.Orders.SingleAsync(item => item.Id == order.Id);
        var persistedReservation = await assertionContext.InventoryReservations
            .SingleAsync(item => item.Id == reservation.Id);
        var persistedProduct = await assertionContext.Products
            .Include(item => item.Inventory)
            .SingleAsync(item => item.Id == product.Id);
        var events = await assertionContext.OutboxMessages
            .Where(message => message.Payload.Contains(order.Id.ToString()))
            .ToArrayAsync();

        persistedOrder.Status.Should().Be(OrderStatus.Cancelled);
        persistedOrder.CancellationReason.Should().Be(OrderCancellationReason.Expired);
        persistedReservation.Status.Should().Be(InventoryReservationStatus.Expired);
        persistedProduct.AvailableStock.Should().Be(10);
        events.Count(message => message.Type == OutBoxMessageType.OrderCancelled).Should().Be(1);
        events.Count(message => message.Type == OutBoxMessageType.StockUpdated).Should().Be(1);
    }

    [PostgreSqlIntegrationFact]
    public async Task OrderExpiration_ConcurrentWithPaymentApproval_ProducesOneConsistentOutcome()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N");
        var customer = User.Create(
            "Race",
            "Customer",
            Email.Create($"expiration-race-{suffix}@example.com").Value!,
            "password-hash").Value!;
        var product = Product.Create(
            $"Race product {suffix}",
            "Payment versus expiration test",
            30m,
            10).Value!;
        product.ReserveStock(3);
        var order = Order.Create(
            customer.Id, now.AddHours(-1), now.AddSeconds(-1)).Value!;
        order.AddItem(product.Id, product.Name, product.Price, 3);
        var reservation = InventoryReservation.Create(
            order.Id, product.Id, product.Inventory.Id, 3,
            order.CreatedAt, order.ExpiresAt).Value!;
        var payment = Payment.Create(
            order.Id, order.Total, "BRL", "ECommercePayment", "race-attempt").Value!;
        payment.RegisterExternalPayment("pay_race");

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Users.AddAsync(customer);
            await seedContext.Products.AddAsync(product);
            await seedContext.Orders.AddAsync(order);
            await seedContext.InventoryReservations.AddAsync(reservation);
            await seedContext.Payments.AddAsync(payment);
            await seedContext.SaveChangesAsync();
        }

        await using var expirationContext = new AppDbContext(options);
        await using var webhookContext = new AppDbContext(options);
        var expirationProcessor = new OrderExpirationProcessor(
            new OrderRepository(expirationContext),
            new InventoryReservationRepository(expirationContext),
            new ProductRepository(expirationContext),
            new OutboxMessageRepository(expirationContext),
            new UnitOfWork(expirationContext),
            new NoOpProductCache());
        var webhookHandler = new ProcessPaymentWebhookHandler(
            new AlwaysValidPaymentWebhookSignatureVerifier(),
            new PaymentRepository(webhookContext),
            new OrderRepository(webhookContext),
            new ProductRepository(webhookContext),
            new InventoryReservationRepository(webhookContext),
            new OutboxMessageRepository(webhookContext),
            new UnitOfWork(webhookContext),
            new NoOpProductCache());
        var webhookPayload = JsonSerializer.Serialize(new
        {
            @event = "payment.approved",
            data = new
            {
                id = "pay_race",
                status = "approved",
                reference = order.Id.ToString(),
                amount = order.Total.ToString(System.Globalization.CultureInfo.InvariantCulture),
                currency = "BRL"
            }
        });

        await Task.WhenAll(
            expirationProcessor.ProcessBatchAsync(10, now),
            WrapWebhookResultAsync(webhookHandler, webhookPayload));

        await using var assertionContext = new AppDbContext(options);
        var persistedOrder = await assertionContext.Orders.SingleAsync(item => item.Id == order.Id);
        var persistedPayment = await assertionContext.Payments.SingleAsync(item => item.Id == payment.Id);
        var persistedReservation = await assertionContext.InventoryReservations
            .SingleAsync(item => item.Id == reservation.Id);
        var persistedProduct = await assertionContext.Products
            .Include(item => item.Inventory)
            .SingleAsync(item => item.Id == product.Id);

        if (persistedOrder.Status == OrderStatus.Paid)
        {
            persistedPayment.Status.Should().Be(PaymentStatus.Paid);
            persistedReservation.Status.Should().Be(InventoryReservationStatus.Consumed);
            persistedProduct.AvailableStock.Should().Be(7);
        }
        else
        {
            persistedOrder.Status.Should().Be(OrderStatus.Cancelled);
            persistedOrder.CancellationReason.Should().Be(OrderCancellationReason.Expired);
            persistedPayment.Status.Should().Be(PaymentStatus.Pending);
            persistedReservation.Status.Should().Be(InventoryReservationStatus.Expired);
            persistedProduct.AvailableStock.Should().Be(10);
        }

        static async Task<Result<OrderExpirationBatchResult>> WrapWebhookResultAsync(
            ProcessPaymentWebhookHandler handler,
            string payload)
        {
            var result = await handler.Handle(
                new ProcessPaymentWebhookCommand(payload, "valid"),
                CancellationToken.None);
            return result.IsSuccess
                ? Result<OrderExpirationBatchResult>.Success(
                    new OrderExpirationBatchResult(0, 0, 0, 0))
                : Result<OrderExpirationBatchResult>.Failure([.. result.Errors]);
        }
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
        var payment = Payment.Create(order.Id, order.Total, "BRL", "integration").Value!;
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
    public async Task UserForUpdate_ConcurrentMutationWaitsAndObservesCommittedState()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var user = User.Create(
            "Concurrent",
            "Customer",
            Email.Create($"user-lock-{Guid.NewGuid():N}@example.com").Value!,
            "hash").Value!;

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Users.AddAsync(user);
            await seedContext.SaveChangesAsync();
        }

        await using var firstContext = new AppDbContext(options);
        var firstUnitOfWork = new UnitOfWork(firstContext);
        var firstRepository = new UserRepository(firstContext);
        await firstUnitOfWork.BeginTransactionAsync();
        var firstMutation = await firstRepository.GetByIdForUpdateAsync(user.Id);
        firstMutation.Should().NotBeNull();

        var secondAttemptStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondMutation = Task.Run(async () =>
        {
            await using var secondContext = new AppDbContext(options);
            var secondUnitOfWork = new UnitOfWork(secondContext);
            var secondRepository = new UserRepository(secondContext);
            await secondUnitOfWork.BeginTransactionAsync();
            secondAttemptStarted.SetResult();
            var lockedUser = await secondRepository.GetByIdForUpdateAsync(user.Id);
            var observedRole = lockedUser!.Role;
            await secondUnitOfWork.CommitTransactionAsync();
            return observedRole;
        });

        await secondAttemptStarted.Task;
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        var secondMutationWasBlocked = !secondMutation.IsCompleted;

        firstMutation!.ChangeRole(UserRole.Administrator).IsSuccess.Should().BeTrue();
        await firstRepository.UpdateAsync(firstMutation);
        await firstUnitOfWork.CommitTransactionAsync();

        var observedRole = await secondMutation;
        secondMutationWasBlocked.Should().BeTrue();
        observedRole.Should().Be(UserRole.Administrator);
    }

    [PostgreSqlIntegrationFact]
    public async Task UserEmailLock_ConcurrentClaimWaitsForFirstTransaction()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var email = $"email-lock-{Guid.NewGuid():N}@example.com";

        await using var firstContext = new AppDbContext(options);
        var firstUnitOfWork = new UnitOfWork(firstContext);
        var firstRepository = new UserRepository(firstContext);
        await firstUnitOfWork.BeginTransactionAsync();
        await firstRepository.AcquireEmailLockAsync(email);

        var secondAttemptStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondClaim = Task.Run(async () =>
        {
            await using var secondContext = new AppDbContext(options);
            var secondUnitOfWork = new UnitOfWork(secondContext);
            var secondRepository = new UserRepository(secondContext);
            await secondUnitOfWork.BeginTransactionAsync();
            secondAttemptStarted.SetResult();
            await secondRepository.AcquireEmailLockAsync(email.ToUpperInvariant());
            await secondUnitOfWork.CommitTransactionAsync();
        });

        await secondAttemptStarted.Task;
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        var secondClaimWasBlocked = !secondClaim.IsCompleted;

        await firstUnitOfWork.CommitTransactionAsync();
        await secondClaim;

        secondClaimWasBlocked.Should().BeTrue();
    }

    [PostgreSqlIntegrationFact]
    public async Task CategoryForUpdate_ConcurrentMutationWaitsAndObservesCommittedState()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var category = Category.Create($"Category lock {Guid.NewGuid():N}").Value!;

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Categories.AddAsync(category);
            await seedContext.SaveChangesAsync();
        }

        await using var firstContext = new AppDbContext(options);
        var firstUnitOfWork = new UnitOfWork(firstContext);
        var firstRepository = new CategoryRepository(firstContext);
        await firstUnitOfWork.BeginTransactionAsync();
        var firstMutation = await firstRepository.GetByIdForUpdateAsync(category.Id);
        firstMutation.Should().NotBeNull();

        var secondAttemptStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondMutation = Task.Run(async () =>
        {
            await using var secondContext = new AppDbContext(options);
            var secondUnitOfWork = new UnitOfWork(secondContext);
            var secondRepository = new CategoryRepository(secondContext);
            await secondUnitOfWork.BeginTransactionAsync();
            secondAttemptStarted.SetResult();
            var lockedCategory = await secondRepository.GetByIdForUpdateAsync(category.Id);
            var observedName = lockedCategory!.Name;
            await secondUnitOfWork.CommitTransactionAsync();
            return observedName;
        });

        await secondAttemptStarted.Task;
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        var secondMutationWasBlocked = !secondMutation.IsCompleted;

        firstMutation!.Update("Serialized category").IsSuccess.Should().BeTrue();
        firstRepository.Update(firstMutation);
        await firstUnitOfWork.CommitTransactionAsync();

        var observedName = await secondMutation;
        secondMutationWasBlocked.Should().BeTrue();
        observedName.Should().Be("Serialized category");
    }

    [PostgreSqlIntegrationFact]
    public async Task CategorySlugLock_ConcurrentClaimWaitsForFirstTransaction()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var slug = $"category-lock-{Guid.NewGuid():N}";

        await using var firstContext = new AppDbContext(options);
        var firstUnitOfWork = new UnitOfWork(firstContext);
        var firstRepository = new CategoryRepository(firstContext);
        await firstUnitOfWork.BeginTransactionAsync();
        await firstRepository.AcquireSlugLockAsync(slug);

        var secondAttemptStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondClaim = Task.Run(async () =>
        {
            await using var secondContext = new AppDbContext(options);
            var secondUnitOfWork = new UnitOfWork(secondContext);
            var secondRepository = new CategoryRepository(secondContext);
            await secondUnitOfWork.BeginTransactionAsync();
            secondAttemptStarted.SetResult();
            await secondRepository.AcquireSlugLockAsync(slug.ToUpperInvariant());
            await secondUnitOfWork.CommitTransactionAsync();
        });

        await secondAttemptStarted.Task;
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        var secondClaimWasBlocked = !secondClaim.IsCompleted;

        await firstUnitOfWork.CommitTransactionAsync();
        await secondClaim;

        secondClaimWasBlocked.Should().BeTrue();
    }

    [PostgreSqlIntegrationFact]
    public async Task PaymentCreationLocks_LoadPaymentAndOutboxUsingPostgreSql()
    {
        var connectionString = await fixture.GetConnectionStringAsync();
        var options = CreateOptions(connectionString);
        var customer = User.Create(
            "Payment",
            "Customer",
            Email.Create($"payment-lock-{Guid.NewGuid():N}@example.com").Value!,
            "hash").Value!;
        var order = Order.Create(customer.Id).Value!;
        var payment = Payment.Create(order.Id, 100m, "BRL", "ECommercePayment").Value!;
        var intention = new OutboxMessage(
            payment.Id,
            OutBoxMessageType.PaymentCreationRequested,
            "{}");

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.AddRangeAsync(customer, order, payment, intention);
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        await unitOfWork.BeginTransactionAsync();

        var lockedPayment = await new PaymentRepository(context).GetByIdForUpdateAsync(payment.Id);
        var lockedIntention = await new OutboxMessageRepository(context)
            .GetByIdForUpdateAsync(intention.Id);

        lockedPayment.Should().NotBeNull();
        lockedIntention.Should().NotBeNull();
        await unitOfWork.RollbackTransactionAsync();
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

    private sealed class PersistedPaymentCreationProcessor(IPaymentRepository payments)
        : IPaymentCreationProcessor
    {
        public async Task<Result<Payment>> ProcessAsync(
            Guid outboxMessageId,
            CancellationToken cancellationToken = default)
        {
            var payment = await payments.GetByIdAsync(outboxMessageId, cancellationToken);
            return payment is null
                ? Result<Payment>.Failure("Payment not found.")
                : Result<Payment>.Success(payment);
        }
    }

    private sealed class AlwaysValidPaymentWebhookSignatureVerifier
        : IPaymentWebhookSignatureVerifier
    {
        public bool IsValid(string payload, string? signature) => true;
    }

    private sealed class TestJwtTokenService : IJwtTokenService
    {
        private readonly string _instanceId = Guid.NewGuid().ToString("N");
        private int _sequence;

        public int AccessTokenExpiresInSeconds => 900;

        public string GenerateAccessToken(User user) => $"access-{user.Id}";

        public string GenerateRefreshToken() =>
            $"rotated-{_instanceId}-{Interlocked.Increment(ref _sequence)}";

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

    private sealed class FixedOrderExpirationPolicy : IOrderExpirationPolicy
    {
        public TimeSpan PaymentLifetime => TimeSpan.FromMinutes(30);
    }
}
