using System.Security.Claims;
using ECommerce.Api.Authorization;
using FluentAssertions;

namespace ECommerce.Api.Tests.Authorization;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void CanAccessUser_Owner_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var principal = CreatePrincipal(userId, "Customer");

        principal.CanAccessUser(userId).Should().BeTrue();
        principal.IsUser(userId).Should().BeTrue();
    }

    [Fact]
    public void CanAccessUser_OtherCustomer_ReturnsFalse()
    {
        var principal = CreatePrincipal(Guid.NewGuid(), "Customer");

        principal.CanAccessUser(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void CanAccessUser_Administrator_ReturnsTrueForAnotherUser()
    {
        var principal = CreatePrincipal(Guid.NewGuid(), "Administrator");

        principal.CanAccessUser(Guid.NewGuid()).Should().BeTrue();
        principal.IsUser(Guid.NewGuid()).Should().BeFalse();
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string role) => new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        ],
        "Test"));
}
