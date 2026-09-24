using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Options;
using ECommerce.Infrastructure.Payments;
using ECommerce.Shared.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Tests.Payments;

public sealed class PaymentOutboxWorkerTests
{
    [Fact]
    public async Task ProcessPendingMessages_ProcessesEveryPaymentCreationIntention()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var repository = new StubOutboxRepository([firstId, secondId]);
        var processor = new RecordingPaymentProcessor();
        var services = new ServiceCollection()
            .AddSingleton<IOutboxMessageRepository>(repository)
            .AddSingleton<IPaymentCreationProcessor>(processor)
            .BuildServiceProvider();
        var worker = new PaymentOutboxWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(
                new OutboxProcessorOptions { BatchSize = 10, PollingIntervalSeconds = 5 }),
            NullLogger<PaymentOutboxWorker>.Instance);

        await worker.ProcessPendingMessagesAsync(CancellationToken.None);

        Assert.Equal([firstId, secondId], processor.ProcessedIds);
        Assert.Equal(OutBoxMessageType.PaymentCreationRequested, repository.RequestedType);
        Assert.Equal(10, repository.RequestedBatchSize);
    }

    private sealed class StubOutboxRepository(IReadOnlyCollection<Guid> pendingIds) : IOutboxMessageRepository
    {
        public OutBoxMessageType RequestedType { get; private set; }
        public int RequestedBatchSize { get; private set; }

        public Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(
            OutBoxMessageType type,
            int take,
            CancellationToken cancellationToken = default)
        {
            RequestedType = type;
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
