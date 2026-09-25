using System.Text.Json;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Users.Dtos;

public sealed record UserAuditEntryDto(
    Guid Id,
    Guid UserId,
    Guid? ActorUserId,
    UserAuditAction Action,
    JsonElement Changes,
    DateTimeOffset OccurredAt);
