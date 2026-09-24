using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.HealthChecks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ECommerce.Persistence.Tests.HealthChecks;

public sealed class PostgresHealthCheckTests
{
    [Fact]
    public async Task CheckHealth_WhenDatabaseCanConnect_ReturnsHealthy()
    {
        await using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var healthCheck = new PostgresHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }
}
