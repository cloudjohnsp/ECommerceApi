using System.Security.Claims;
using ECommerce.Domain.Enums;

namespace ECommerce.Api.Authorization;

public static class ClaimsPrincipalExtensions
{
    public static bool CanAccessUser(this ClaimsPrincipal principal, Guid userId) =>
        principal.IsInRole(nameof(UserRole.Administrator)) || principal.IsUser(userId);

    public static bool IsUser(this ClaimsPrincipal principal, Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var authenticatedUserId) &&
        authenticatedUserId == userId;
}
