using ECommerce.Application.Abstractions.Messaging;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Messaging;
using ECommerce.Infrastructure.Options;
using ECommerce.Shared.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Infrastructure.Tests.Messaging;

public sealed class IntegrationEventOutboxWorkerTests
{
    [Fact]
    public async Task ProcessPendingMessages_PollsIntegrationEventTypesAndProcessesTheirMessages()
    {
        var messageId = Guid.NewGuid();
        var repository = new RecordingOutboxRepository(messageId);
        var processor = new RecordingProcessor();
        var services = new ServiceCollection()
            .AddSingleton<IOutboxMessageRepository>(repository)
            .AddSingleton<IIntegrationEventOutboxProcessor>(processor)
            .BuildServiceProvider();
        var worker = new IntegrationEventOutboxWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(
                new OutboxProcessorOptions { BatchSize = 10, PollingIntervalSeconds = 5 }),
            NullLogger<IntegrationEventOutboxWorker>.Instance);

        await worker.ProcessPendingMessagesAsync(CancellationToken.None);

        Assert.Equal(
            [OutBoxMessageType.OrderCreated, OutBoxMessageType.OrderUpdated, OutBoxMessageType.OrderDeleted],
            repository.RequestedTypes);
        Assert.Equal([messageId], processor.ProcessedIds);
    }

    private sealed class RecordingOutboxRepository(Guid orderCreatedMessageId) : IOutboxMessageRepository
    {
        public List<OutBoxMessageType> RequestedTypes { get; } = [];

        public Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(
            OutBoxMessageType type,
            int take,
            CancellationToken cancellationToken = default)
        {
            RequestedTypes.Add(type);
            IReadOnlyCollection<Guid> ids = type == OutBoxMessageType.OrderCreated
                ? [orderCreatedMessageId]
                : [];
            return Task.FromResult(ids);
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

        public Task<Result> ProcessAsync(Guid outboxMessageId, CancellationToken cancellationToken = default)
        {
            ProcessedIds.Add(outboxMessageId);
            return Task.FromResult(Result.Success());
        }
    }
}
