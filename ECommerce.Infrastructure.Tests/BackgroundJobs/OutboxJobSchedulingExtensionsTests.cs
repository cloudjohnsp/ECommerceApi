using ECommerce.Infrastructure.BackgroundJobs;
using ECommerce.Infrastructure.Email;
using ECommerce.Infrastructure.Messaging;
using ECommerce.Infrastructure.Options;
using ECommerce.Infrastructure.Payments;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace ECommerce.Infrastructure.Tests.BackgroundJobs;

public sealed class BackgroundJobSchedulingExtensionsTests
{
    [Fact]
    public void ScheduleBackgroundJobs_WhenEnabled_RegistersAllRecurringJobs()
    {
        var manager = new Mock<IRecurringJobManager>();
        var services = new ServiceCollection()
            .AddSingleton(manager.Object)
            .AddSingleton<IOptions<OutboxProcessorOptions>>(
                Microsoft.Extensions.Options.Options.Create(new OutboxProcessorOptions
                {
                    Enabled = true,
                    CronExpression = "*/5 * * * *"
                }))
            .AddSingleton<IOptions<OrderExpirationOptions>>(
                Microsoft.Extensions.Options.Options.Create(new OrderExpirationOptions
                {
                    Enabled = true,
                    CronExpression = "*/5 * * * *"
                }))
            .BuildServiceProvider();

        services.ScheduleBackgroundJobs();

        VerifyJob<PaymentOutboxJob>(manager, "outbox:payments");
        VerifyJob<UserEmailOutboxJob>(manager, "outbox:user-emails");
        VerifyJob<IntegrationEventOutboxJob>(manager, "outbox:integration-events");
        VerifyJob<OrderExpirationJob>(manager, "orders:expiration");
    }

    [Fact]
    public void ScheduleBackgroundJobs_WhenDisabled_DoesNotRequireHangfireServices()
    {
        var services = new ServiceCollection()
            .AddSingleton<IOptions<OutboxProcessorOptions>>(
                Microsoft.Extensions.Options.Options.Create(
                    new OutboxProcessorOptions { Enabled = false }))
            .AddSingleton<IOptions<OrderExpirationOptions>>(
                Microsoft.Extensions.Options.Options.Create(
                    new OrderExpirationOptions { Enabled = false }))
            .BuildServiceProvider();

        var action = () => services.ScheduleBackgroundJobs();

        action.Should().NotThrow();
    }

    private static void VerifyJob<TJob>(Mock<IRecurringJobManager> manager, string jobId)
    {
        manager.Verify(item => item.AddOrUpdate(
            jobId,
            It.Is<Job>(job => job.Type == typeof(TJob)),
            "*/5 * * * *",
            It.Is<RecurringJobOptions>(options => options.TimeZone == TimeZoneInfo.Utc)), Times.Once);
    }
}
