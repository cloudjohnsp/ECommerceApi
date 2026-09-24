using ECommerce.Persistence.Contexts;
using ECommerce.Persistence.Options;
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
        if (!options.ApplyMigrationsOnStartup)
            return;

        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseMigration");

        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations applied successfully.");
                return;
            }
            catch (Exception exception) when (attempt < options.MaxAttempts)
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
