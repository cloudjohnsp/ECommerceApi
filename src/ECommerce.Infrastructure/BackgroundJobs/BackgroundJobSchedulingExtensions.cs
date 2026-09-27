using ECommerce.Infrastructure.Email;
using ECommerce.Infrastructure.Messaging;
using ECommerce.Infrastructure.Options;
using ECommerce.Infrastructure.Payments;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.BackgroundJobs;

public static class BackgroundJobSchedulingExtensions
{
    public static IServiceProvider ScheduleBackgroundJobs(this IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<OutboxProcessorOptions>>().Value;
        var expirationOptions = serviceProvider
            .GetRequiredService<IOptions<OrderExpirationOptions>>().Value;
        if (!options.Enabled && !expirationOptions.Enabled) return serviceProvider;

        var recurringJobs = serviceProvider.GetRequiredService<IRecurringJobManager>();
        var recurringOptions = new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc };

        if (options.Enabled)
        {
            recurringJobs.AddOrUpdate<PaymentOutboxJob>(
                "outbox:payments",
                job => job.ExecuteAsync(CancellationToken.None),
                options.CronExpression,
                recurringOptions);
            recurringJobs.AddOrUpdate<UserEmailOutboxJob>(
                "outbox:user-emails",
                job => job.ExecuteAsync(CancellationToken.None),
                options.CronExpression,
                recurringOptions);
            recurringJobs.AddOrUpdate<IntegrationEventOutboxJob>(
                "outbox:integration-events",
                job => job.ExecuteAsync(CancellationToken.None),
                options.CronExpression,
                recurringOptions);
        }
        if (expirationOptions.Enabled)
        {
            recurringJobs.AddOrUpdate<OrderExpirationJob>(
                "orders:expiration",
                job => job.ExecuteAsync(CancellationToken.None),
                expirationOptions.CronExpression,
                recurringOptions);
        }

        return serviceProvider;
    }
}
