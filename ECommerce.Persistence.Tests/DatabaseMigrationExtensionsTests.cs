using ECommerce.Persistence.Options;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Persistence.Tests;

public sealed class DatabaseMigrationExtensionsTests
{
    [Fact]
    public async Task ApplyDatabaseMigrations_WhenDisabled_DoesNotRequireDbContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<DatabaseInitializationOptions>()
            .Configure(options => { });
        await using var provider = services.BuildServiceProvider();

        var action = () => provider.ApplyDatabaseMigrationsAsync();

        await action.Should().NotThrowAsync();
    }
}
