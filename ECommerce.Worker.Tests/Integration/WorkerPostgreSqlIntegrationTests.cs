using System.Text.Json;
using ECommerce.Shared.Messaging;
using ECommerce.Worker.Persistence;
using ECommerce.Worker.Processing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ECommerce.Worker.Tests.Integration;

public sealed class WorkerPostgreSqlIntegrationTests
{
    [WorkerPostgreSqlIntegrationFact]
    public async Task MigrationsAndEventProcessing_PersistAtomicWorkerState()
    {
        await using var container = new PostgreSqlBuilder("postgres:18.6-alpine3.23")
            .WithDatabase("ecommerce_worker_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<WorkerDbContext>()
            .UseNpgsql(container.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", WorkerDbContext.SchemaName))
            .Options;

        await using var context = new WorkerDbContext(options);
        await context.Database.MigrateAsync();
        var payload = new OrderIntegrationEventPayload(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@example.com",
            "Pending",
            99.90m,
            DateTimeOffset.UtcNow,
            [new OrderIntegrationEventItem(Guid.NewGuid(), "Product", 99.90m, 1, 99.90m)]);
        var processor = new OrderIntegrationEventProcessor(context);

        await processor.ProcessAsync(
            Guid.NewGuid(),
            "order.created",
            JsonSerializer.SerializeToUtf8Bytes(payload));
        await processor.ProcessAsync(
            Guid.NewGuid(),
            "order.paid",
            JsonSerializer.SerializeToUtf8Bytes(payload with
            {
                CustomerEmail = null,
                Status = "Paid",
                OccurredAt = payload.OccurredAt.AddSeconds(1)
            }));
        var stockPayload = new StockUpdatedIntegrationEventPayload(
            payload.Items.First().ProductId,
            9,
            payload.OrderId,
            StockUpdateReasons.Reserved,
            payload.OccurredAt.AddSeconds(2));
        var stockProcessor = new StockIntegrationEventProcessor(context);
        await stockProcessor.ProcessAsync(
            Guid.NewGuid(),
            StockIntegrationEventProcessor.EventType,
            JsonSerializer.SerializeToUtf8Bytes(stockPayload));

        (await context.ConsumedIntegrationEvents.CountAsync()).Should().Be(3);
        (await context.NotificationOutboxMessages.CountAsync()).Should().Be(2);
        (await context.Invoices.CountAsync()).Should().Be(1);
        (await context.InventoryProjections.SingleAsync()).AvailableStock.Should().Be(9);

        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'worker';",
            connection);
        Convert.ToInt32(await command.ExecuteScalarAsync()).Should().BeGreaterThanOrEqualTo(6);
    }
}

public sealed class WorkerPostgreSqlIntegrationFactAttribute : FactAttribute
{
    public WorkerPostgreSqlIntegrationFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_POSTGRES_INTEGRATION_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set RUN_POSTGRES_INTEGRATION_TESTS=true and start Docker to run this test.";
        }
    }
}
