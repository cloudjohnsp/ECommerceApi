using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Users;
using ECommerce.Application.Users.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Users.Handlers;

public sealed class DeleteUserHandlerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserAuditRepository> _auditRepository = new();

    [Fact]
    public async Task Handle_WithExistingUser_DeactivatesUserAndCommits()
    {
        var user = UserFactory.Create();
        var command = new DeleteUserCommand(user.Id);
        _userRepository
            .Setup(repository => repository.GetByIdForUpdateAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var handler = new DeleteUserHandler(
            _userRepository.Object,
            _refreshTokenRepository.Object,
            _unitOfWork.Object,
            _auditRepository.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.DeactivatedAt.Should().NotBeNull();
        _userRepository.Verify(repository => repository.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepository.Verify(repository => repository.RevokeAllForUserAsync(
            user.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        _auditRepository.Verify(repository => repository.AddAsync(
            It.Is<UserAuditEntry>(entry =>
                entry.UserId == user.Id && entry.Action == UserAuditAction.Deactivated),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistingUser_ReturnsFailureWithoutUpdating()
    {
        var command = new DeleteUserCommand(Guid.NewGuid());
        _userRepository
            .Setup(repository => repository.GetByIdForUpdateAsync(command.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new DeleteUserHandler(
            _userRepository.Object,
            _refreshTokenRepository.Object,
            _unitOfWork.Object,
            _auditRepository.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("User not found.");
        _userRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(repository => repository.RevokeAllForUserAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
