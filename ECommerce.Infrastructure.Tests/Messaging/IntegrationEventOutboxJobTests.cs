using ECommerce.Application.Abstractions.Messaging;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Messaging;
using ECommerce.Infrastructure.Options;
using ECommerce.Shared.Results;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Infrastructure.Tests.Messaging;

public sealed class IntegrationEventOutboxJobTests
{
    [Fact]
    public async Task ProcessPendingMessages_PollsIntegrationEventTypesAndProcessesTheirMessages()
    {
        var messageId = Guid.NewGuid();
        var repository = new RecordingOutboxRepository(messageId);
        var processor = new RecordingProcessor();
        var job = new IntegrationEventOutboxJob(
            repository,
            processor,
            Microsoft.Extensions.Options.Options.Create(
                new OutboxProcessorOptions { BatchSize = 10 }),
            NullLogger<IntegrationEventOutboxJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(
            [
                OutBoxMessageType.OrderCreated,
                OutBoxMessageType.OrderUpdated,
                OutBoxMessageType.OrderDeleted,
                OutBoxMessageType.OrderPaid,
                OutBoxMessageType.PaymentFailed,
                OutBoxMessageType.OrderRefunded,
                OutBoxMessageType.OrderCancelled
            ],
            repository.RequestedTypes);
        Assert.Equal([messageId], processor.ProcessedIds);
    }

    [Fact]
    public async Task ProcessPendingMessages_WhenPublicationFails_StopsChronologicalBatch()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var repository = new RecordingOutboxRepository(firstId, secondId);
        var processor = new RecordingProcessor { FailingMessageId = firstId };
        var job = new IntegrationEventOutboxJob(
            repository,
            processor,
            Microsoft.Extensions.Options.Options.Create(
                new OutboxProcessorOptions { BatchSize = 10 }),
            NullLogger<IntegrationEventOutboxJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal([firstId], processor.ProcessedIds);
    }

    private sealed class RecordingOutboxRepository(params Guid[] pendingMessageIds) : IOutboxMessageRepository
    {
        public List<OutBoxMessageType> RequestedTypes { get; } = [];

        public Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(
            IReadOnlyCollection<OutBoxMessageType> types,
            int take,
            CancellationToken cancellationToken = default)
        {
            RequestedTypes.AddRange(types);
            return Task.FromResult<IReadOnlyCollection<Guid>>(pendingMessageIds);
        }

        public Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<OutboxMessage?>(null);

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Update(OutboxMessage message)
        {
        }
    }

    private sealed class RecordingProcessor : IIntegrationEventOutboxProcessor
    {
        public List<Guid> ProcessedIds { get; } = [];
        public Guid? FailingMessageId { get; init; }

        public Task<Result> ProcessAsync(Guid outboxMessageId, CancellationToken cancellationToken = default)
        {
            ProcessedIds.Add(outboxMessageId);
            return Task.FromResult(outboxMessageId == FailingMessageId
                ? Result.Failure("Broker unavailable.")
                : Result.Success());
        }
    }
}
