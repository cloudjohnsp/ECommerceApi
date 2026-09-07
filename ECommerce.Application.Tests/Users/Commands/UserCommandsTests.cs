using ECommerce.Application.Users;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using FluentAssertions;

namespace ECommerce.Application.Tests.Users.Commands;


public class UserCommandsTests
{
    [Fact]
    public void CreateUserCommand_ReturnsRecord()
    {
        // Arrange
        CreateUserCommand user = new("John", "Doe", "john.doe@example.com", "123456@password", UserRole.Customer);
        // Assert
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.Email.Should().Be("john.doe@example.com");
        user.Password.Should().Be("123456@password");
        user.Role.Should().Be(UserRole.Customer);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Administrator)]
    public void CreateUserCommand_WithValidRole_RetunsRecord(UserRole role)
    {
        // Arrange
        CreateUserCommand user = new("John", "Doe", "john.doe@example.com", "123456@password", role);
        // Assert
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.Email.Should().Be("john.doe@example.com");
        user.Password.Should().Be("123456@password");
        user.Role.Should().Be(role);
    }

    [Fact]
    public void DeleteUserCommand_ReturnsRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        DeleteUserCommand command = new(userId);
        // Assert
        command.UserId.Should().Be(userId);
    }
}
