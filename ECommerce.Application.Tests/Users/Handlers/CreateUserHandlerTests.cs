using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Users;
using ECommerce.Application.Users.Handlers;
using ECommerce.Domain.Enums;
using FluentAssertions;
using Moq;

namespace ECommerce.Domain.Tests.Handlers;

public sealed class CreateUserHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();

    [Fact]
    public async Task Handle_WithValidCommand_CreatesUserAndCommits()
    {
        var command = new CreateUserCommand(
            "Jane",
            "Doe",
            "jane.doe@example.com",
            "Password1!",
            UserRole.Customer);

        _userRepository
            .Setup(repository => repository.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _passwordHasher
            .Setup(hasher => hasher.HashPassword(command.Password))
            .Returns("hashed-password");
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new CreateUserHandler(_userRepository.Object, _unitOfWork.Object, _passwordHasher.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.FirstName.Should().Be("Jane");
        result.Value.LastName.Should().Be("Doe");
        result.Value.Email.Should().Be("jane.doe@example.com");
        result.Value.Role.Should().Be(UserRole.Customer);
        result.Value.IsActive.Should().BeTrue();

        _passwordHasher.Verify(hasher => hasher.HashPassword(command.Password), Times.Once);
        _userRepository.Verify(repository => repository.AddAsync(
            It.IsAny<Entities.User>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ReturnsFailureWithoutPersisting()
    {
        var command = new CreateUserCommand("Jane", "Doe", "invalid-email", "Password1!");
        var handler = new CreateUserHandler(_userRepository.Object, _unitOfWork.Object, _passwordHasher.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("E-mail is invalid.");
        _userRepository.Verify(repository => repository.ExistsByEmailAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _passwordHasher.Verify(hasher => hasher.HashPassword(It.IsAny<string>()), Times.Never);
        _userRepository.Verify(repository => repository.AddAsync(
            It.IsAny<Entities.User>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithRegisteredEmail_ReturnsFailureWithoutCreatingUser()
    {
        var command = new CreateUserCommand("Jane", "Doe", "jane.doe@example.com", "Password1!");

        _userRepository
            .Setup(repository => repository.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new CreateUserHandler(_userRepository.Object, _unitOfWork.Object, _passwordHasher.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("E-mail is already registered.");
        _passwordHasher.Verify(hasher => hasher.HashPassword(It.IsAny<string>()), Times.Never);
        _userRepository.Verify(repository => repository.AddAsync(
            It.IsAny<Entities.User>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }
}