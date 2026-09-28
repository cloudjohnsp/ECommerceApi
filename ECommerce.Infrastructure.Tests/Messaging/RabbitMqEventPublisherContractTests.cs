using System.Text;
using ECommerce.Application.Abstractions.Messaging;
using ECommerce.Infrastructure.Messaging;
using FluentAssertions;
using System.Text.Json;
using ECommerce.Shared.Messaging;
using System.Text.Json.Nodes;

namespace ECommerce.Infrastructure.Tests.Messaging;

public sealed class RabbitMqEventPublisherContractTests
{
    [Fact]
    public void CreateProperties_UsesVersion1TransportContract()
    {
        var occurredAt = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        var integrationEvent = new IntegrationEvent(
            Guid.NewGuid(),
            "order.created",
            "{}",
            occurredAt,
            "checkout-123");

        var properties = RabbitMqEventPublisher.CreateProperties(integrationEvent);

        properties.ContentType.Should().Be("application/json");
        properties.MessageId.Should().Be(integrationEvent.Id.ToString());
        properties.Type.Should().Be(integrationEvent.Type);
        properties.Timestamp.UnixTime.Should().Be(new DateTimeOffset(occurredAt).ToUnixTimeSeconds());
        properties.Headers.Should().ContainKey(RabbitMqEventPublisher.ContractVersionHeaderName);
        Encoding.UTF8.GetString((byte[])properties.Headers![RabbitMqEventPublisher.ContractVersionHeaderName]!)
            .Should().Be(RabbitMqEventPublisher.ContractVersion);
        Encoding.UTF8.GetString((byte[])properties.Headers!["x-correlation-id"]!)
            .Should().Be("checkout-123");

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<JsonElement>>(
            RabbitMqEventPublisher.CreateBody(integrationEvent));
        envelope.Should().NotBeNull();
        envelope!.MessageId.Should().Be(integrationEvent.Id);
        envelope.EventType.Should().Be("order.created");
        envelope.Version.Should().Be(IntegrationEventContract.Version1);
        envelope.OccurredAt.Should().Be(new DateTimeOffset(occurredAt));
        envelope.CorrelationId.Should().Be("checkout-123");
        envelope.Payload.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public void SerializedEnvelope_MatchesCanonicalVersion1Example()
    {
        var root = FindSolutionRoot();
        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(
            root,
            "docs",
            "contracts",
            "v1",
            "order-paid.example.json")))!;
        var payload = expected["payload"]!.ToJsonString();
        var integrationEvent = new IntegrationEvent(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "order.paid",
            payload,
            new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc),
            "checkout-123");

        var actual = JsonNode.Parse(RabbitMqEventPublisher.CreateBody(integrationEvent));

        JsonNode.DeepEquals(actual, expected).Should().BeTrue();
    }

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ECommerceApi.slnx")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the solution root.");
    }
}
