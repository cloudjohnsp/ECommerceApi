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
        Status = OutBoxMessageStatus.Pending;
    }

    public void MarkProcessed()
    {
        Status = OutBoxMessageStatus.Processed;
        UpdatedAt = DateTime.UtcNow;
    }
}
