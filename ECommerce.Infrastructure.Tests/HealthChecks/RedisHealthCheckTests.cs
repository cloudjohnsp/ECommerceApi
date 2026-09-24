using ECommerce.Infrastructure.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace ECommerce.Infrastructure.Tests.HealthChecks;

public sealed class RedisHealthCheckTests
{
    [Fact]
    public async Task CheckHealth_WhenRedisResponds_ReturnsHealthy()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(instance => instance.GetAsync("health:redis", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        var healthCheck = new RedisHealthCheck(cache.Object);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealth_WhenRedisFails_ReturnsUnhealthy()
    {
        var cache = new Mock<IDistributedCache>();
        cache.Setup(instance => instance.GetAsync("health:redis", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis unavailable"));
        var healthCheck = new RedisHealthCheck(cache.Object);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }
}
