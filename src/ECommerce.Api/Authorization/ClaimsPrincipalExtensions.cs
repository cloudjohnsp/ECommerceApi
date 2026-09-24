using System.Security.Claims;
using ECommerce.Domain.Enums;

namespace ECommerce.Api.Authorization;

public static class ClaimsPrincipalExtensions
{
    public static bool CanAccessUser(this ClaimsPrincipal principal, Guid userId) =>
        principal.IsInRole(nameof(UserRole.Administrator)) || principal.IsUser(userId);

    public static bool IsUser(this ClaimsPrincipal principal, Guid userId) =>
        principal.TryGetUserId(out var authenticatedUserId) && authenticatedUserId == userId;

    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public static bool TryGetCustomerScope(this ClaimsPrincipal principal, out Guid? customerId)
    {
        if (principal.IsInRole(nameof(UserRole.Administrator)))
        {
            customerId = null;
            return true;
        }

        if (principal.TryGetUserId(out var authenticatedUserId))
        {
            customerId = authenticatedUserId;
            return true;
        }

        customerId = null;
        return false;
    }
}
