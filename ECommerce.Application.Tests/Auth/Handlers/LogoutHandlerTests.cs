using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth;
using ECommerce.Application.Auth.Handlers;
using ECommerce.Domain.Entities;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Auth.Handlers;

public sealed class LogoutHandlerTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();

    [Fact]
    public async Task Handle_WithKnownToken_RevokesAllUserTokensAndCommitsTransaction()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "token-hash", DateTimeOffset.UtcNow.AddMinutes(10));
        var command = new LogoutCommand("refresh-token");
        _jwtTokenService
            .Setup(service => service.HashRefreshToken(command.RefreshToken))
            .Returns("token-hash");
        _refreshTokenRepository
            .Setup(repository => repository.GetByTokenHashForUpdateAsync("token-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var handler = new LogoutHandler(
            _refreshTokenRepository.Object,
            _unitOfWork.Object,
            _jwtTokenService.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _refreshTokenRepository.Verify(repository => repository.RevokeAllForUserAsync(
            token.UserId,
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithUnknownToken_ReturnsSuccessWithoutCommitting()
    {
        var command = new LogoutCommand("unknown-token");
        _jwtTokenService
            .Setup(service => service.HashRefreshToken(command.RefreshToken))
            .Returns("unknown-hash");
        _refreshTokenRepository
            .Setup(repository => repository.GetByTokenHashForUpdateAsync("unknown-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var handler = new LogoutHandler(
            _refreshTokenRepository.Object,
            _unitOfWork.Object,
            _jwtTokenService.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExpiredKnownToken_StillRevokesAllUserTokens()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "expired-hash", DateTimeOffset.UtcNow.AddMinutes(-1));
        var command = new LogoutCommand("expired-token");
        _jwtTokenService
            .Setup(service => service.HashRefreshToken(command.RefreshToken))
            .Returns("expired-hash");
        _refreshTokenRepository
            .Setup(repository => repository.GetByTokenHashForUpdateAsync("expired-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var handler = new LogoutHandler(
            _refreshTokenRepository.Object,
            _unitOfWork.Object,
            _jwtTokenService.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _refreshTokenRepository.Verify(repository => repository.RevokeAllForUserAsync(
            token.UserId,
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
