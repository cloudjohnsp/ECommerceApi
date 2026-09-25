using ECommerce.Infrastructure.Email;
using ECommerce.Infrastructure.Messaging;
using ECommerce.Infrastructure.Options;
using ECommerce.Infrastructure.Payments;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.BackgroundJobs;

public static class OutboxJobSchedulingExtensions
{
    public static IServiceProvider ScheduleOutboxJobs(this IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<OutboxProcessorOptions>>().Value;
        if (!options.Enabled)
            return serviceProvider;

        var recurringJobs = serviceProvider.GetRequiredService<IRecurringJobManager>();
        var recurringOptions = new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc };

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

        return serviceProvider;
    }
}
