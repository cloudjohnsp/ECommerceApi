using System.Text;
using ECommerce.Application.Abstractions.Messaging;
using ECommerce.Infrastructure.Messaging;
using FluentAssertions;

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
            occurredAt);

        var properties = RabbitMqEventPublisher.CreateProperties(integrationEvent);

        properties.ContentType.Should().Be("application/json");
        properties.MessageId.Should().Be(integrationEvent.Id.ToString());
        properties.Type.Should().Be(integrationEvent.Type);
        properties.Timestamp.UnixTime.Should().Be(new DateTimeOffset(occurredAt).ToUnixTimeSeconds());
        properties.Headers.Should().ContainKey(RabbitMqEventPublisher.ContractVersionHeaderName);
        Encoding.UTF8.GetString((byte[])properties.Headers![RabbitMqEventPublisher.ContractVersionHeaderName]!)
            .Should().Be(RabbitMqEventPublisher.ContractVersion);
    }
}
