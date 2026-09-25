using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth;
using ECommerce.Application.Auth.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Auth.Handlers;

public sealed class LoginHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenService> _jwt = new();

    [Fact]
    public async Task Handle_WithUnconfirmedEmail_ReturnsFailureWithoutIssuingTokens()
    {
        var user = UserFactory.Create();
        _users.Setup(repository => repository.GetByEmailAsync(
                user.Email.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyPassword("Password1!", user.PasswordHash))
            .Returns(true);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new LoginCommand(user.Email.Value, "Password1!"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("E-mail is not confirmed.");
        _refreshTokens.Verify(repository => repository.AddAsync(
            It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithConfirmedEmail_IssuesTokens()
    {
        var user = UserFactory.Create();
        user.ConfirmEmail();
        _users.Setup(repository => repository.GetByEmailAsync(
                user.Email.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyPassword("Password1!", user.PasswordHash))
            .Returns(true);
        _jwt.Setup(service => service.GenerateAccessToken(user)).Returns("access-token");
        _jwt.Setup(service => service.GenerateRefreshToken()).Returns("refresh-token");
        _jwt.Setup(service => service.HashRefreshToken("refresh-token")).Returns("refresh-hash");
        _jwt.Setup(service => service.GetRefreshTokenExpiresAt())
            .Returns(DateTimeOffset.UtcNow.AddDays(1));
        _jwt.Setup(service => service.AccessTokenExpiresInSeconds).Returns(3600);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new LoginCommand(user.Email.Value, "Password1!"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("access-token");
        _refreshTokens.Verify(repository => repository.AddAsync(
            It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private LoginHandler CreateHandler() => new(
        _users.Object,
        _refreshTokens.Object,
        _unitOfWork.Object,
        _passwordHasher.Object,
        _jwt.Object);
}
