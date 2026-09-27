using System.Diagnostics.Metrics;
using ECommerce.Application.Abstractions.Orders;
using ECommerce.Infrastructure.BackgroundJobs;
using ECommerce.Infrastructure.Options;
using ECommerce.Shared.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ECommerce.Infrastructure.Tests.BackgroundJobs;

public sealed class OrderExpirationJobTests
{
    [Fact]
    public async Task Execute_RecordsExpiredCountAndDelayMetrics()
    {
        var processor = new Mock<IOrderExpirationProcessor>();
        processor.Setup(item => item.ProcessBatchAsync(
                12,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderExpirationBatchResult>.Success(
                new OrderExpirationBatchResult(3, 2, 1, 45)));
        var measurements = new List<(string Name, double Value)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == OrderExpirationJob.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            measurements.Add((instrument.Name, value)));
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
            measurements.Add((instrument.Name, value)));
        listener.Start();
        var job = CreateJob(processor, 12);

        await job.ExecuteAsync(CancellationToken.None);

        measurements.Should().Contain(("ecommerce.orders.expired", 2));
        measurements.Should().Contain(("ecommerce.order_expiration.delay", 45));
    }

    [Fact]
    public async Task Execute_WhenProcessorFails_ThrowsForHangfireRetryAndRecordsFailure()
    {
        var processor = new Mock<IOrderExpirationProcessor>();
        processor.Setup(item => item.ProcessBatchAsync(
                It.IsAny<int>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<OrderExpirationBatchResult>.Failure("database unavailable"));
        var failures = new List<long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Name == "ecommerce.order_expiration.failures")
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => failures.Add(value));
        listener.Start();
        var job = CreateJob(processor, 10);

        var action = () => job.ExecuteAsync(CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*database unavailable*");
        failures.Should().ContainSingle().Which.Should().Be(1);
    }

    private static OrderExpirationJob CreateJob(
        Mock<IOrderExpirationProcessor> processor,
        int batchSize) => new(
        processor.Object,
        Microsoft.Extensions.Options.Options.Create(
            new OrderExpirationOptions { BatchSize = batchSize }),
        NullLogger<OrderExpirationJob>.Instance);
}
