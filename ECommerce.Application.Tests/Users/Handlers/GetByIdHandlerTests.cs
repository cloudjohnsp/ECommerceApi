using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Users;
using ECommerce.Application.Users.Dtos;
using ECommerce.Application.Users.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Tests.Users.Handlers;

public sealed class GetByIdHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();

    [Fact]
    public async Task Handle_WithExistingUser_ReturnsUserDto()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        var user = UserFactory.Create();
        var query = new GetUserByIdQuery(userId);
        _userRepository.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new GetUserByIdHandler(_userRepository.Object);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(user.Id);
        result.Value.FirstName.Should().Be(user.FirstName);
        result.Value.LastName.Should().Be(user.LastName);
        result.Value.Email.Should().Be(user.Email.Value);
        result.Value.IsActive.Should().Be(user.IsActive);
        result.Value.Role.Should().Be(user.Role);
    }

    [Fact]
    public async Task Handle_WithNonExistingUser_ReturnsFailureResult()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        var query = new GetUserByIdQuery(userId);
        _userRepository.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new GetUserByIdHandler(_userRepository.Object);
        // Act
        var result = await handler.Handle(query, CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("User not found.");
    }
}
