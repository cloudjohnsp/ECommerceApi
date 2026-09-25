using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Options;
using ECommerce.Infrastructure.Payments;
using ECommerce.Shared.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Tests.Payments;

public sealed class PaymentOutboxJobTests
{
    [Fact]
    public async Task ProcessPendingMessages_ProcessesEveryPaymentCreationIntention()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var repository = new StubOutboxRepository([firstId, secondId]);
        var processor = new RecordingPaymentProcessor();
        var job = new PaymentOutboxJob(
            repository,
            processor,
            Microsoft.Extensions.Options.Options.Create(
                new OutboxProcessorOptions { BatchSize = 10 }),
            NullLogger<PaymentOutboxJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal([firstId, secondId], processor.ProcessedIds);
        Assert.Equal([OutBoxMessageType.PaymentCreationRequested], repository.RequestedTypes);
        Assert.Equal(10, repository.RequestedBatchSize);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesExceptionForHangfireRetry()
    {
        var repository = new StubOutboxRepository([]) { Exception = new TimeoutException("Database timeout.") };
        var job = new PaymentOutboxJob(
            repository,
            new RecordingPaymentProcessor(),
            Microsoft.Extensions.Options.Options.Create(new OutboxProcessorOptions()),
            NullLogger<PaymentOutboxJob>.Instance);

        var action = () => job.ExecuteAsync(CancellationToken.None);

        await Assert.ThrowsAsync<TimeoutException>(action);
    }

    private sealed class StubOutboxRepository(IReadOnlyCollection<Guid> pendingIds) : IOutboxMessageRepository
    {
        public IReadOnlyCollection<OutBoxMessageType> RequestedTypes { get; private set; } = [];
        public int RequestedBatchSize { get; private set; }
        public Exception? Exception { get; init; }

        public Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(
            IReadOnlyCollection<OutBoxMessageType> types,
            int take,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
                return Task.FromException<IReadOnlyCollection<Guid>>(Exception);

            RequestedTypes = types;
            RequestedBatchSize = take;
            return Task.FromResult(pendingIds);
        }

        public Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<OutboxMessage?>(null);

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Update(OutboxMessage message)
        {
        }
    }

    private sealed class RecordingPaymentProcessor : IPaymentCreationProcessor
    {
        public List<Guid> ProcessedIds { get; } = [];

        public Task<Result<Payment>> ProcessAsync(
            Guid outboxMessageId,
            CancellationToken cancellationToken = default)
        {
            ProcessedIds.Add(outboxMessageId);
            return Task.FromResult(Result<Payment>.Failure("Retry later."));
        }
    }
}
