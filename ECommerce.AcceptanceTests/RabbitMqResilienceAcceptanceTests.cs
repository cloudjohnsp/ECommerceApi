using System.Text;
using System.Text.Json;
using FluentAssertions;
using RabbitMQ.Client;

namespace ECommerce.AcceptanceTests;

[Collection(CrossServiceCollection.Name)]
public sealed class RabbitMqResilienceAcceptanceTests(CrossServiceFixture fixture)
{
    [CrossServiceFact]
    public async Task DuplicateTransientAndInvalidDeliveries_AreObservableAndHandled()
    {
        var duplicateMessageId = Guid.NewGuid();
        var duplicateOrderId = Guid.NewGuid();
        var duplicateBody = CreateEnvelope(
            duplicateMessageId,
            "order.created",
            CreateOrderPayload(duplicateOrderId, includeEmail: true));

        await PublishAsync(duplicateMessageId, "order.created", duplicateBody);
        await PublishAsync(duplicateMessageId, "order.created", duplicateBody);

        await fixture.EventuallyAsync(async () =>
            await fixture.SqlAsync($$"""
                SELECT count(*) FROM worker.consumed_integration_events
                WHERE "MessageId" = '{{duplicateMessageId}}';
                """) == "1",
            "duplicate deliveries to result in one inbox record");

        var transientMessageId = Guid.NewGuid();
        await PublishAsync(
            transientMessageId,
            "order.updated",
            CreateEnvelope(
                transientMessageId,
                "order.updated",
                CreateOrderPayload(Guid.NewGuid(), includeEmail: false)));

        var invalidMessageId = Guid.NewGuid();
        await PublishAsync(
            invalidMessageId,
            "order.created",
            Encoding.UTF8.GetBytes("{not-json"),
            version: "1");

        await fixture.EventuallyAsync(async () =>
        {
            var result = await fixture.ComposeCommandAsync(
                "exec", "-T", "rabbitmq", "rabbitmqctl", "list_queues",
                "--quiet", "name", "messages_ready");
            return result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Any(line => line.StartsWith("ecommerce.worker.orders.dead", StringComparison.Ordinal) &&
                             line.EndsWith("\t2", StringComparison.Ordinal));
        }, "the invalid event and exhausted transient failure to reach the DLQ", TimeSpan.FromSeconds(30));

        var deadLetters = await ReadDeadLettersAsync(2);
        deadLetters.Should().Contain(letter =>
            letter.MessageId == invalidMessageId &&
            letter.FailureReason == "invalid_event" &&
            letter.RetryCount == 0);
        deadLetters.Should().Contain(letter =>
            letter.MessageId == transientMessageId &&
            letter.FailureReason == "retry_exhausted" &&
            letter.RetryCount == 2);
    }

    private async Task PublishAsync(
        Guid messageId,
        string eventType,
        ReadOnlyMemory<byte> body,
        string version = "1")
    {
        var factory = CreateConnectionFactory();
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(true, true));
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = messageId.ToString(),
            Type = eventType,
            Headers = new Dictionary<string, object?>
            {
                ["x-contract-version"] = Encoding.UTF8.GetBytes(version),
                ["x-correlation-id"] = Encoding.UTF8.GetBytes(messageId.ToString("N"))
            }
        };
        await channel.BasicPublishAsync(
            "ecommerce.events",
            eventType,
            mandatory: true,
            properties,
            body);
    }

    private async Task<IReadOnlyCollection<DeadLetter>> ReadDeadLettersAsync(int count)
    {
        var result = new List<DeadLetter>();
        var factory = CreateConnectionFactory();
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        for (var index = 0; index < count; index++)
        {
            var delivery = await channel.BasicGetAsync("ecommerce.worker.orders.dead", autoAck: true);
            delivery.Should().NotBeNull();
            var headers = delivery!.BasicProperties.Headers;
            result.Add(new DeadLetter(
                Guid.Parse(delivery.BasicProperties.MessageId!),
                HeaderText(headers, "x-failure-reason"),
                HeaderInt(headers, "x-retry-count")));
        }
        return result;
    }

    private ConnectionFactory CreateConnectionFactory() => new()
    {
        HostName = "127.0.0.1",
        Port = fixture.RabbitPort,
        UserName = "acceptance",
        Password = "acceptance-password",
        VirtualHost = "/"
    };

    private static byte[] CreateEnvelope(Guid messageId, string eventType, object payload) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            messageId,
            eventType,
            version = "1",
            occurredAt = DateTimeOffset.UtcNow,
            correlationId = messageId.ToString("N"),
            payload
        });

    private static object CreateOrderPayload(Guid orderId, bool includeEmail) => new
    {
        orderId,
        customerId = Guid.NewGuid(),
        customerEmail = includeEmail ? $"{Guid.NewGuid():N}@example.test" : null,
        status = "Pending",
        total = 10m,
        expiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
        cancellationReason = (string?)null,
        occurredAt = DateTimeOffset.UtcNow,
        items = Array.Empty<object>()
    };

    private static string HeaderText(IDictionary<string, object?>? headers, string name) =>
        headers is not null && headers.TryGetValue(name, out var value)
            ? value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                ReadOnlyMemory<byte> bytes => Encoding.UTF8.GetString(bytes.Span),
                _ => value?.ToString() ?? string.Empty
            }
            : string.Empty;

    private static int HeaderInt(IDictionary<string, object?>? headers, string name)
    {
        var value = headers is not null && headers.TryGetValue(name, out var raw) ? raw : null;
        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number => checked((int)number),
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var number) => number,
            _ => -1
        };
    }

    private sealed record DeadLetter(Guid MessageId, string FailureReason, int RetryCount);
}
