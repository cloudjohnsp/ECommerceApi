using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Support;

public static class UserFactory
{
    public static Email CreateEmail(string value = "jane.doe@example.com")
    {
        var result = Email.Create(value);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }

    public static User Create(
        string firstName = "Jane",
        string lastName = "Doe",
        Email? email = null,
        string passwordHash = "hashed-password",
        UserRole role = UserRole.Customer)
    {
        var result = User.Create(firstName, lastName, email ?? CreateEmail(), passwordHash, role);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }
}
