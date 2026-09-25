using System.Text.Json;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Users;

internal static class UserAuditEntryFactory
{
    public static UserAuditEntry Created(User user) =>
        Create(user.Id, null, UserAuditAction.Created, new Dictionary<string, object?>
        {
            ["role"] = Change(null, user.Role.ToString())
        });

    public static UserAuditEntry ProfileUpdated(
        User user,
        Guid? actorUserId,
        string previousFirstName,
        string previousLastName,
        string previousEmail)
    {
        var changes = new Dictionary<string, object?>();
        AddChange(changes, "firstName", previousFirstName, user.FirstName);
        AddChange(changes, "lastName", previousLastName, user.LastName);
        AddChange(changes, "email", previousEmail, user.Email.Value);
        return Create(user.Id, actorUserId, UserAuditAction.ProfileUpdated, changes);
    }

    public static UserAuditEntry PasswordChanged(User user, Guid? actorUserId) =>
        Create(user.Id, actorUserId, UserAuditAction.PasswordChanged, new Dictionary<string, object?>());

    public static UserAuditEntry RoleChanged(
        User user,
        Guid? actorUserId,
        UserRole previousRole) =>
        Create(user.Id, actorUserId, UserAuditAction.RoleChanged, new Dictionary<string, object?>
        {
            ["role"] = Change(previousRole.ToString(), user.Role.ToString())
        });

    public static UserAuditEntry EmailConfirmed(User user) =>
        Create(user.Id, null, UserAuditAction.EmailConfirmed, new Dictionary<string, object?>());

    public static UserAuditEntry Deactivated(User user, Guid? actorUserId) =>
        Create(user.Id, actorUserId, UserAuditAction.Deactivated, new Dictionary<string, object?>());

    private static UserAuditEntry Create(
        Guid userId,
        Guid? actorUserId,
        UserAuditAction action,
        IReadOnlyDictionary<string, object?> changes) =>
        new(userId, actorUserId, action, JsonSerializer.Serialize(changes));

    private static void AddChange(
        IDictionary<string, object?> changes,
        string field,
        string previous,
        string current)
    {
        if (!string.Equals(previous, current, StringComparison.Ordinal))
            changes[field] = Change(previous, current);
    }

    private static IReadOnlyDictionary<string, string?> Change(string? previous, string? current) =>
        new Dictionary<string, string?>
        {
            ["from"] = previous,
            ["to"] = current
        };
}
