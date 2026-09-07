using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Users;
using ECommerce.Application.Users.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Users.Handlers;

public sealed class ChangeUserPasswordHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();

    [Fact]
    public async Task Handle_WithExistingUser_HashesPasswordUpdatesUserAndCommits()
    {
        var user = UserFactory.Create();
        var command = new ChangeUserPasswordCommand(user.Id, "NewPassword1!");
        _userRepository
            .Setup(repository => repository.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(hasher => hasher.HashPassword(command.Password!))
            .Returns("new-hashed-password");
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new ChangeUserPasswordHandler(
            _userRepository.Object,
            _unitOfWork.Object,
            _passwordHasher.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        user.PasswordHash.Should().Be("new-hashed-password");
        _passwordHasher.Verify(hasher => hasher.HashPassword(command.Password!), Times.Once);
        _userRepository.Verify(repository => repository.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingUser_ReturnsFailureWithoutHashingOrUpdating()
    {
        var command = new ChangeUserPasswordCommand(Guid.NewGuid(), "NewPassword1!");
        _userRepository
            .Setup(repository => repository.GetByIdAsync(command.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new ChangeUserPasswordHandler(
            _userRepository.Object,
            _unitOfWork.Object,
            _passwordHasher.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("User not found.");
        _passwordHasher.Verify(hasher => hasher.HashPassword(It.IsAny<string>()), Times.Never);
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }
}
