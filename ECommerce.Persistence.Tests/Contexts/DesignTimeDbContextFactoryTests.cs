using ECommerce.Persistence.Contexts;
using FluentAssertions;

namespace ECommerce.Persistence.Tests.Contexts;

public sealed class DesignTimeDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WithoutSecretConfiguration_UsesNpgsqlForModelTooling()
    {
        var previousValue = Environment.GetEnvironmentVariable(
            DesignTimeDbContextFactory.ConnectionStringEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                DesignTimeDbContextFactory.ConnectionStringEnvironmentVariable,
                null);
            var factory = new DesignTimeDbContextFactory();

            using var context = factory.CreateDbContext([]);

            context.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                DesignTimeDbContextFactory.ConnectionStringEnvironmentVariable,
                previousValue);
        }
    }
}
