using System.Diagnostics.Metrics;
using ECommerce.Application.Abstractions.Orders;
using ECommerce.Infrastructure.Options;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.BackgroundJobs;

public sealed class OrderExpirationJob(
    IOrderExpirationProcessor processor,
    IOptions<OrderExpirationOptions> options,
    ILogger<OrderExpirationJob> logger)
{
    public const string MeterName = "ECommerce.OrderExpiration";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> ExpiredOrders =
        Meter.CreateCounter<long>("ecommerce.orders.expired");
    private static readonly Counter<long> Failures =
        Meter.CreateCounter<long>("ecommerce.order_expiration.failures");
    private static readonly Histogram<double> ProcessingDelay =
        Meter.CreateHistogram<double>(
            "ecommerce.order_expiration.delay",
            unit: "s");

    private readonly OrderExpirationOptions _options = options.Value;

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await processor.ProcessBatchAsync(
                _options.BatchSize,
                DateTimeOffset.UtcNow,
                cancellationToken);
            if (result.IsFailure)
            {
                var errors = string.Join("; ", result.Errors);
                logger.LogError("Order expiration batch failed: {Errors}", errors);
                throw new InvalidOperationException($"Order expiration batch failed: {errors}");
            }

            var batch = result.Value!;
            ExpiredOrders.Add(batch.ExpiredCount);
            ProcessingDelay.Record(batch.MaximumDelaySeconds);
            logger.LogInformation(
                "Order expiration batch examined {CandidateCount} candidates, expired {ExpiredCount} and skipped {SkippedCount}",
                batch.CandidateCount,
                batch.ExpiredCount,
                batch.SkippedCount);
        }
        catch
        {
            Failures.Add(1);
            throw;
        }
    }
}
