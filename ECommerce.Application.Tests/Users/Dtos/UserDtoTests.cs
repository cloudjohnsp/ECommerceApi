using ECommerce.Application.Users.Dtos;
using ECommerce.Domain.Enums;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Tests.Users.Dtos;

public class UserDtoTests
{
    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Administrator)]
    public void UserDto_ReturnsRecord(UserRole role)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userDto = new UserDto(userId, "John", "Doe", "john.doe@example.com", role, true);
        // Assert
        userDto.Id.Should().Be(userId);
        userDto.FirstName.Should().Be("John");
        userDto.LastName.Should().Be("Doe");
        userDto.Email.Should().Be("john.doe@example.com");
        userDto.Role.Should().Be(role);
    }
}
