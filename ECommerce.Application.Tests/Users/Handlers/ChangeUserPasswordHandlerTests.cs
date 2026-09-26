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
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUserAuditRepository> _auditRepository = new();

    [Fact]
    public async Task Handle_WithExistingUser_HashesPasswordUpdatesUserAndCommits()
    {
        var user = UserFactory.Create();
        var originalPasswordHash = user.PasswordHash;
        var command = new ChangeUserPasswordCommand(user.Id, "CurrentPassword1!", "NewPassword1!");
        _userRepository
            .Setup(repository => repository.GetByIdForUpdateAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(hasher => hasher.VerifyPassword(command.CurrentPassword!, originalPasswordHash))
            .Returns(true);
        _passwordHasher
            .Setup(hasher => hasher.HashPassword(command.NewPassword!))
            .Returns("new-hashed-password");
        var handler = new ChangeUserPasswordHandler(
            _userRepository.Object,
            _refreshTokenRepository.Object,
            _unitOfWork.Object,
            _passwordHasher.Object,
            _auditRepository.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        user.PasswordHash.Should().Be("new-hashed-password");
        _passwordHasher.Verify(hasher => hasher.VerifyPassword(
            command.CurrentPassword!,
            originalPasswordHash), Times.Once);
        _passwordHasher.Verify(hasher => hasher.HashPassword(command.NewPassword!), Times.Once);
        _userRepository.Verify(repository => repository.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepository.Verify(repository => repository.RevokeAllForUserAsync(
            user.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        _auditRepository.Verify(repository => repository.AddAsync(
            It.Is<UserAuditEntry>(entry =>
                entry.UserId == user.Id &&
                entry.Action == ECommerce.Domain.Enums.UserAuditAction.PasswordChanged &&
                !entry.ChangesJson.Contains("NewPassword", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidCurrentPassword_ReturnsFailureWithoutMutation()
    {
        var user = UserFactory.Create();
        var originalPasswordHash = user.PasswordHash;
        var command = new ChangeUserPasswordCommand(user.Id, "WrongPassword1!", "NewPassword1!");
        _userRepository
            .Setup(repository => repository.GetByIdForUpdateAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher
            .Setup(hasher => hasher.VerifyPassword(command.CurrentPassword!, originalPasswordHash))
            .Returns(false);

        var handler = new ChangeUserPasswordHandler(
            _userRepository.Object,
            _refreshTokenRepository.Object,
            _unitOfWork.Object,
            _passwordHasher.Object,
            _auditRepository.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle("Current password is invalid.");
        user.PasswordHash.Should().Be(originalPasswordHash);
        _passwordHasher.Verify(hasher => hasher.HashPassword(It.IsAny<string>()), Times.Never);
        _userRepository.Verify(repository => repository.UpdateAsync(
            It.IsAny<User>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(repository => repository.RevokeAllForUserAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _auditRepository.Verify(repository => repository.AddAsync(
            It.IsAny<UserAuditEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistingUser_ReturnsFailureWithoutHashingOrUpdating()
    {
        var command = new ChangeUserPasswordCommand(
            Guid.NewGuid(),
            "CurrentPassword1!",
            "NewPassword1!");
        _userRepository
            .Setup(repository => repository.GetByIdForUpdateAsync(command.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new ChangeUserPasswordHandler(
            _userRepository.Object,
            _refreshTokenRepository.Object,
            _unitOfWork.Object,
            _passwordHasher.Object,
            _auditRepository.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("User not found.");
        _passwordHasher.Verify(hasher => hasher.VerifyPassword(
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
        _passwordHasher.Verify(hasher => hasher.HashPassword(It.IsAny<string>()), Times.Never);
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(repository => repository.RevokeAllForUserAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
