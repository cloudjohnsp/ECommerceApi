using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Email;
using ECommerce.Infrastructure.Options;
using ECommerce.Shared.Results;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Infrastructure.Tests.Email;

public sealed class UserEmailOutboxJobTests
{
    [Fact]
    public async Task ProcessPendingMessages_PollsBothEmailTypesAndProcessesMessages()
    {
        var confirmationId = Guid.NewGuid();
        var resetId = Guid.NewGuid();
        var repository = new RecordingOutboxRepository(confirmationId, resetId);
        var processor = new RecordingProcessor();
        var job = new UserEmailOutboxJob(
            repository,
            processor,
            Microsoft.Extensions.Options.Options.Create(
                new OutboxProcessorOptions { BatchSize = 10 }),
            NullLogger<UserEmailOutboxJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Equal(
            [OutBoxMessageType.EmailConfirmationRequested, OutBoxMessageType.PasswordResetRequested],
            repository.RequestedTypes);
        Assert.Equal([confirmationId, resetId], processor.ProcessedIds);
    }

    private sealed class RecordingOutboxRepository(Guid confirmationId, Guid resetId)
        : IOutboxMessageRepository
    {
        public List<OutBoxMessageType> RequestedTypes { get; } = [];

        public Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(
            IReadOnlyCollection<OutBoxMessageType> types,
            int take,
            CancellationToken cancellationToken = default)
        {
            RequestedTypes.AddRange(types);
            return Task.FromResult<IReadOnlyCollection<Guid>>([confirmationId, resetId]);
        }

        public Task<OutboxMessage?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<OutboxMessage?>(null);

        public Task<OutboxMessage?> GetByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<OutboxMessage?>(null);

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Update(OutboxMessage message)
        {
        }
    }

    private sealed class RecordingProcessor : IUserEmailOutboxProcessor
    {
        public List<Guid> ProcessedIds { get; } = [];

        public Task<Result> ProcessAsync(
            Guid outboxMessageId,
            CancellationToken cancellationToken = default)
        {
            ProcessedIds.Add(outboxMessageId);
            return Task.FromResult(Result.Success());
        }
    }
}
