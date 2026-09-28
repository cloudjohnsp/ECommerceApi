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
    public string CorrelationId { get; private set; } = null!;

    private OutboxMessage() { }

    public OutboxMessage(OutBoxMessageType type, string data, string? correlationId = null)
        : this(Guid.NewGuid(), type, data, correlationId)
    {
    }

    public OutboxMessage(Guid id, OutBoxMessageType type, string data, string? correlationId = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Outbox message id is required.", nameof(id));
        Id = id;
        Type = type;
        Payload = data;
        CreatedAt = DateTime.UtcNow;
        NextAttemptAt = CreatedAt;
        Status = OutBoxMessageStatus.Pending;
        CorrelationId = NormalizeCorrelationId(correlationId) ?? id.ToString("N");
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

    public void AssignCorrelationId(string? correlationId)
    {
        var normalized = NormalizeCorrelationId(correlationId);
        if (normalized is not null)
            CorrelationId = normalized;
    }

    private static string? NormalizeCorrelationId(string? correlationId)
    {
        var normalized = correlationId?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized[..Math.Min(128, normalized.Length)];
    }
}
