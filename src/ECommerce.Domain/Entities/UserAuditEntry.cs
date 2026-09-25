using System.Text;
using System.Text.Json;
using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities;

public sealed class UserAuditEntry : Entity
{
    public const int MaximumChangesSizeBytes = 16 * 1024;

    public Guid UserId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public UserAuditAction Action { get; private set; }
    public string ChangesJson { get; private set; } = "{}";
    public DateTimeOffset OccurredAt { get; private set; }

    private UserAuditEntry()
    {
    }

    public UserAuditEntry(
        Guid userId,
        Guid? actorUserId,
        UserAuditAction action,
        string changesJson)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Audited user ID is required.", nameof(userId));
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("Actor user ID cannot be empty.", nameof(actorUserId));
        if (!Enum.IsDefined(action))
            throw new ArgumentOutOfRangeException(nameof(action), action, "Audit action is invalid.");
        ValidateChanges(changesJson);

        UserId = userId;
        ActorUserId = actorUserId;
        Action = action;
        ChangesJson = changesJson;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateChanges(string changesJson)
    {
        if (string.IsNullOrWhiteSpace(changesJson))
            throw new ArgumentException("Audit changes are required.", nameof(changesJson));
        if (Encoding.UTF8.GetByteCount(changesJson) > MaximumChangesSizeBytes)
            throw new ArgumentException("Audit changes exceed the maximum size.", nameof(changesJson));

        try
        {
            using var document = JsonDocument.Parse(changesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Audit changes must be a JSON object.", nameof(changesJson));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Audit changes must contain valid JSON.", nameof(changesJson), exception);
        }
    }
}
