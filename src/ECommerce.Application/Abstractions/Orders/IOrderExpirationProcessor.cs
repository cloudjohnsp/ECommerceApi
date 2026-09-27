using ECommerce.Shared.Results;

namespace ECommerce.Application.Abstractions.Orders;

public interface IOrderExpirationProcessor
{
    Task<Result<OrderExpirationBatchResult>> ProcessBatchAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed record OrderExpirationBatchResult(
    int CandidateCount,
    int ExpiredCount,
    int SkippedCount,
    double MaximumDelaySeconds);
