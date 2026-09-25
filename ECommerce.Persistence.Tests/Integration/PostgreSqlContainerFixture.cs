using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ECommerce.Persistence.Tests.Integration;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task<string> GetConnectionStringAsync()
    {
        if (_connectionString is not null)
            return _connectionString;

        await _initializationLock.WaitAsync();
        try
        {
            if (_connectionString is not null)
                return _connectionString;

            _container = new PostgreSqlBuilder("postgres:18.6-alpine3.23")
                .WithDatabase("ecommerce_tests")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _container.StartAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .Options;
            await using var context = new AppDbContext(options);
            await context.Database.MigrateAsync();

            _connectionString = _container.GetConnectionString();
            return _connectionString;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();

        _initializationLock.Dispose();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlIntegrationCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string Name = "PostgreSQL integration";
}

public sealed class PostgreSqlIntegrationFactAttribute : FactAttribute
{
    public PostgreSqlIntegrationFactAttribute()
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
