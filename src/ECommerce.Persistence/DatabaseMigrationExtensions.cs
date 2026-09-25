using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Options;
using ECommerce.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Persistence;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var options = services.GetRequiredService<IOptions<DatabaseInitializationOptions>>().Value;
        var seedOptions = services.GetRequiredService<IOptions<DatabaseSeedOptions>>().Value;
        if (!options.ApplyMigrationsOnStartup && !seedOptions.Enabled)
            return;

        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseMigration");

        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (options.ApplyMigrationsOnStartup)
                    await dbContext.Database.MigrateAsync(cancellationToken);

                if (seedOptions.Enabled)
                    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>()
                        .SeedAsync(cancellationToken);

                logger.LogInformation("Database initialization completed successfully.");
                return;
            }
            catch (Exception exception) when (
                exception is not DatabaseSeedConflictException &&
                attempt < options.MaxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds} seconds.",
                    attempt,
                    options.MaxAttempts,
                    options.RetryDelaySeconds);
                await Task.Delay(
                    TimeSpan.FromSeconds(options.RetryDelaySeconds),
                    cancellationToken);
            }
        }
    }
}
