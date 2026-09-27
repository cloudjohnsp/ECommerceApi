using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public OutBoxMessageType Type { get; private set; }
    public string Payload { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; set; }
    public OutBoxMessageStatus Status { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public Guid? LockId { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(OutBoxMessageType type, string data)
        : this(Guid.NewGuid(), type, data)
    {
    }

    public OutboxMessage(Guid id, OutBoxMessageType type, string data)
    {
        if (id == Guid.Empty) throw new ArgumentException("Outbox message id is required.", nameof(id));
        Id = id;
        Type = type;
        Payload = data;
        CreatedAt = DateTime.UtcNow;
        NextAttemptAt = CreatedAt;
        Status = OutBoxMessageStatus.Pending;
    }

    public void MarkProcessed(bool clearPayload = false)
    {
        Status = OutBoxMessageStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
        LockId = null;
        LockedUntil = null;
        LastError = null;
        if (clearPayload)
            Payload = "{}";
        UpdatedAt = ProcessedAt;
    }
}
