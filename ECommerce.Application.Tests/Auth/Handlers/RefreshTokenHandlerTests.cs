using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth;
using ECommerce.Application.Auth.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Auth.Handlers;

public sealed class RefreshTokenHandlerTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();

    [Fact]
    public async Task Handle_WithUnknownToken_ReturnsFailure()
    {
        var command = new RefreshTokenCommand("unknown-token");
        _jwtTokenService
            .Setup(service => service.HashRefreshToken(command.RefreshToken))
            .Returns("unknown-hash");
        _refreshTokenRepository
            .Setup(repository => repository.GetByTokenHashAsync("unknown-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var handler = CreateHandler();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Invalid refresh token.");
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithRevokedToken_RevokesAllUserTokensAndReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var token = RefreshToken.Create(userId, "revoked-hash", DateTimeOffset.UtcNow.AddMinutes(10));
        token.Revoke();
        var command = new RefreshTokenCommand("revoked-token");
        _jwtTokenService
            .Setup(service => service.HashRefreshToken(command.RefreshToken))
            .Returns("revoked-hash");
        _refreshTokenRepository
            .Setup(repository => repository.GetByTokenHashAsync("revoked-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Invalid refresh token.");
        _refreshTokenRepository.Verify(repository => repository.RevokeAllForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsFailureWithoutCommitting()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "expired-hash", DateTimeOffset.UtcNow.AddMinutes(-1));
        var command = new RefreshTokenCommand("expired-token");
        _jwtTokenService
            .Setup(service => service.HashRefreshToken(command.RefreshToken))
            .Returns("expired-hash");
        _refreshTokenRepository
            .Setup(repository => repository.GetByTokenHashAsync("expired-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var handler = CreateHandler();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Invalid refresh token.");
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ReturnsFailureWithoutRotatingToken()
    {
        var user = UserFactory.Create();
        user.Deactivate();
        var token = RefreshToken.Create(user.Id, "valid-hash", DateTimeOffset.UtcNow.AddMinutes(10));
        var command = new RefreshTokenCommand("refresh-token");
        SetupTokenLookup(command, token);
        _userRepository
            .Setup(repository => repository.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = CreateHandler();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Invalid refresh token.");
        _refreshTokenRepository.Verify(repository => repository.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidToken_RotatesTokenAndCommits()
    {
        var user = UserFactory.Create();
        var token = RefreshToken.Create(user.Id, "valid-hash", DateTimeOffset.UtcNow.AddMinutes(10));
        var command = new RefreshTokenCommand("refresh-token");
        SetupTokenLookup(command, token);
        _userRepository
            .Setup(repository => repository.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _jwtTokenService
            .Setup(service => service.GenerateAccessToken(user))
            .Returns("access-token");
        _jwtTokenService
            .Setup(service => service.GenerateRefreshToken())
            .Returns("new-refresh-token");
        _jwtTokenService
            .Setup(service => service.GetRefreshTokenExpiresAt())
            .Returns(DateTimeOffset.UtcNow.AddMinutes(30));
        _jwtTokenService
            .Setup(service => service.AccessTokenExpiresInSeconds)
            .Returns(3600);
        _unitOfWork
            .Setup(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new
        {
            AccessToken = "access-token",
            RefreshToken = "new-refresh-token",
            ExpiresIn = 3600
        });
        token.RevokedAt.Should().NotBeNull();
        token.ReplacedByTokenId.Should().NotBeNull();
        _refreshTokenRepository.Verify(repository => repository.AddAsync(
            It.Is<RefreshToken>(refreshToken => refreshToken.UserId == user.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unitOfWork => unitOfWork.Commit(It.IsAny<CancellationToken>()), Times.Once);
    }

    private RefreshTokenHandler CreateHandler()
    {
        return new RefreshTokenHandler(
            _refreshTokenRepository.Object,
            _userRepository.Object,
            _unitOfWork.Object,
            _jwtTokenService.Object);
    }

    private void SetupTokenLookup(RefreshTokenCommand command, RefreshToken token)
    {
        _jwtTokenService
            .Setup(service => service.HashRefreshToken(command.RefreshToken))
            .Returns("valid-hash");
        _refreshTokenRepository
            .Setup(repository => repository.GetByTokenHashAsync("valid-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _jwtTokenService
            .Setup(service => service.HashRefreshToken(It.IsAny<string>()))
            .Returns("new-hash");
        _jwtTokenService
            .Setup(service => service.HashRefreshToken(command.RefreshToken))
            .Returns("valid-hash");
    }
}
