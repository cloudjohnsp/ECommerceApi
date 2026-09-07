using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Users;
using ECommerce.Application.Users.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Tests.Users.Handlers;

public sealed class UpdateUserProfileHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Handle_WithValidRequest_UpdatesUserProfile()
    {
        // Arrange
        var user = UserFactory.Create();
        var request = new UpdateUserProfileCommand(user.Id, "John", "Doe", "john.doe@example.com");
        _userRepository.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _unitOfWork.Setup(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new UpdateUserProfileHandler(_userRepository.Object, _unitOfWork.Object);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.FirstName.Should().Be("John");
        result.Value.LastName.Should().Be("Doe");
        result.Value.Email.Should().Be("john.doe@example.com");
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ReturnsValidationError()
    {
        // Arrange
        var user = UserFactory.Create();
        var request = new UpdateUserProfileCommand(user.Id, "John", "Doe", "invalid-email");
        _userRepository.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var handler = new UpdateUserProfileHandler(_userRepository.Object, _unitOfWork.Object);
        // Act
        var result = await handler.Handle(request, CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("E-mail is invalid.");
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidUserId_ReturnsUserNotFound()
    {
        // Arrange
        var request = new UpdateUserProfileCommand(Guid.NewGuid(), "John", "Doe", "john.doe@example.com");
        _userRepository.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new UpdateUserProfileHandler(_userRepository.Object, _unitOfWork.Object);
        // Act
        var result = await handler.Handle(request, CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("User not found.");
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyFirstName_ReturnsValidationError()
    {
        // Arrange
        var user = UserFactory.Create();
        var request = new UpdateUserProfileCommand(user.Id, "", "Doe", "john.doe@example.com");
        _userRepository.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var handler = new UpdateUserProfileHandler(_userRepository.Object, _unitOfWork.Object);
        // Act
        var result = await handler.Handle(request, CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("First name must contain between 1 and 100 characters.");
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidFirstName_ReturnsValidationError()
    {
        // Arrange
        var user = UserFactory.Create();
        var request = new UpdateUserProfileCommand(user.Id, "J@hn", "Doe", "john.doe@example.com");
        _userRepository.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var handler = new UpdateUserProfileHandler(_userRepository.Object, _unitOfWork.Object);
        // Act
        var result = await handler.Handle(request, CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("First name contains invalid characters.");
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidLastName_ReturnsValidationError()
    {
        // Arrange
        var user = UserFactory.Create();
        var request = new UpdateUserProfileCommand(user.Id, "John", "D0e", "john.doe@example.com");
        _userRepository.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var handler = new UpdateUserProfileHandler(_userRepository.Object, _unitOfWork.Object);
        // Act
        var result = await handler.Handle(request, CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Last name contains invalid characters.");
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyLastName_ReturnsValidationError()
    {
        // Arrange
        var user = UserFactory.Create();
        var request = new UpdateUserProfileCommand(user.Id, "John", "", "john.doe@example.com");
        _userRepository.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var handler = new UpdateUserProfileHandler(_userRepository.Object, _unitOfWork.Object);
        // Act
        var result = await handler.Handle(request, CancellationToken.None);
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Last name must contain between 1 and 100 characters.");
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
