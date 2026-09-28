using FluentAssertions;

namespace ECommerce.AcceptanceTests;

[Collection(CrossServiceCollection.Name)]
public sealed class PublishInterruptionAcceptanceTests(CrossServiceFixture fixture)
{
    [CrossServiceFact]
    public async Task WorkerInterruptedAfterBrokerConfirmation_ReclaimsAndCompletesOutboxIdempotently()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var customerId = await fixture.CreateCustomerAsync($"interrupt-{suffix}");
        var productId = await fixture.CreateProductAsync(discriminator: $"interrupt-{suffix}");

        await fixture.SqlAsync("""
            CREATE OR REPLACE FUNCTION acceptance_delay_outbox_confirmation()
            RETURNS trigger AS $$
            BEGIN
                IF NEW."Status" = 2 AND OLD."Status" = 1 THEN
                    PERFORM pg_sleep(60);
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            DROP TRIGGER IF EXISTS acceptance_delay_outbox_confirmation
                ON "OutboxMessages";
            CREATE TRIGGER acceptance_delay_outbox_confirmation
                BEFORE UPDATE ON "OutboxMessages"
                FOR EACH ROW
                EXECUTE FUNCTION acceptance_delay_outbox_confirmation();
            """);

        Guid orderId = default;
        string? outboxId = null;
        try
        {
            orderId = await fixture.CreateOrderAsync(customerId, productId);

            await fixture.EventuallyAsync(async () =>
            {
                outboxId = await fixture.SqlAsync($$"""
                    SELECT o."Id"
                    FROM "OutboxMessages" o
                    JOIN worker.consumed_integration_events c ON c."MessageId" = o."Id"
                    WHERE o."Type" = 1
                      AND o."Payload"::jsonb ->> 'OrderId' = '{{orderId}}'
                      AND o."Status" = 1
                      AND o."ProcessedAt" IS NULL;
                    """);
                return Guid.TryParse(outboxId, out _);
            }, "RabbitMQ consumption while the API outbox confirmation is blocked");

            await fixture.EventuallyAsync(async () =>
            {
                var publishingBackendCount = await fixture.SqlAsync("""
                    SELECT count(*)
                    FROM pg_stat_activity
                    WHERE datname = 'ecommerce'
                      AND wait_event = 'PgSleep'
                      AND query LIKE '%OutboxMessages%';
                    """);
                return int.TryParse(publishingBackendCount, out var count) && count > 0;
            }, "the database confirmation statement to enter its controlled failure window");

            await fixture.ComposeCommandAsync("kill", "worker");

            (await fixture.SqlAsync("""
                SELECT bool_and(pg_terminate_backend(pid))
                FROM pg_stat_activity
                WHERE datname = 'ecommerce'
                  AND wait_event = 'PgSleep'
                  AND query LIKE '%OutboxMessages%';
                """)).Should().Be("t", "all in-flight confirmation transactions must be aborted");

        }
        finally
        {
            await fixture.SqlAsync("""
                DROP TRIGGER IF EXISTS acceptance_delay_outbox_confirmation
                    ON "OutboxMessages";
                DROP FUNCTION IF EXISTS acceptance_delay_outbox_confirmation();
                """);
            await fixture.ComposeCommandAsync(
                "up", "--detach", "--no-deps", "--scale", "worker=2", "worker");
        }

        Guid.TryParse(outboxId, out var parsedOutboxId).Should().BeTrue();
        await fixture.EventuallyAsync(async () =>
            await fixture.SqlAsync($$"""
                SELECT "Status" || ':' || ("ProcessedAt" IS NOT NULL)::int || ':' || "Attempts"
                FROM "OutboxMessages"
                WHERE "Id" = '{{parsedOutboxId}}';
                """) is var state &&
            state.Split(':') is var parts &&
            parts.Length == 3 &&
            parts[0] == "2" &&
            parts[1] == "1" &&
            int.Parse(parts[2]) >= 2,
            "the expired lease to be reclaimed, republished and confirmed",
            TimeSpan.FromSeconds(45));

        (await fixture.SqlAsync($$"""
            SELECT count(*)
            FROM worker.consumed_integration_events
            WHERE "MessageId" = '{{parsedOutboxId}}';
            """)).Should().Be("1", "redelivery must remain idempotent in the Worker inbox");
    }
}
