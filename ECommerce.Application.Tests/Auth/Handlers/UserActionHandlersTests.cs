using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth;
using ECommerce.Application.Auth.Handlers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Tests.Support;
using FluentAssertions;
using Moq;

namespace ECommerce.Application.Tests.Auth.Handlers;

public sealed class UserActionHandlersTests
{
    private const string TokenHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private readonly Mock<IUserActionTokenRepository> _tokens = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IOutboxMessageRepository> _outbox = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUserActionTokenService> _tokenService = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserAuditRepository> _audit = new();

    public UserActionHandlersTests()
    {
        _tokenService.Setup(service => service.Hash("raw-token")).Returns(TokenHash);
        _tokenService.Setup(service => service.Issue())
            .Returns(new IssuedUserActionToken("raw-token", TokenHash));
    }

    [Fact]
    public async Task ConfirmEmail_WithValidToken_ConfirmsUserAndConsumesTokens()
    {
        var user = UserFactory.Create();
        var token = CreateToken(user.Id, UserActionTokenType.EmailConfirmation);
        _tokens.Setup(repository => repository.GetByHashForUpdateAsync(
                TokenHash, UserActionTokenType.EmailConfirmation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _users.Setup(repository => repository.GetByIdAsync(
                user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var handler = new ConfirmEmailHandler(
            _tokens.Object, _users.Object, _tokenService.Object, _unitOfWork.Object, _audit.Object);

        var result = await handler.Handle(new ConfirmEmailCommand("raw-token"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.IsEmailConfirmed.Should().BeTrue();
        _tokens.Verify(repository => repository.ConsumeActiveForUserAsync(
            user.Id,
            UserActionTokenType.EmailConfirmation,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unit => unit.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(repository => repository.AddAsync(
            It.Is<UserAuditEntry>(entry => entry.Action == UserAuditAction.EmailConfirmed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_ReturnsSuccessWithoutPersisting()
    {
        var handler = new ForgotPasswordHandler(
            _users.Object,
            _tokens.Object,
            _outbox.Object,
            _tokenService.Object,
            _unitOfWork.Object);

        var result = await handler.Handle(
            new ForgotPasswordCommand("unknown@example.com"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _tokens.Verify(repository => repository.AddAsync(
            It.IsAny<UserActionToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unit => unit.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_WithConfirmedUser_CreatesTokenAndOutboxMessage()
    {
        var user = UserFactory.Create();
        user.ConfirmEmail();
        _users.Setup(repository => repository.GetByEmailForUpdateAsync(
                user.Email.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var handler = new ForgotPasswordHandler(
            _users.Object,
            _tokens.Object,
            _outbox.Object,
            _tokenService.Object,
            _unitOfWork.Object);

        var result = await handler.Handle(
            new ForgotPasswordCommand(user.Email.Value),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _tokens.Verify(repository => repository.AddAsync(
            It.Is<UserActionToken>(token => token.Type == UserActionTokenType.PasswordReset),
            It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(repository => repository.AddAsync(
            It.Is<OutboxMessage>(message => message.Type == OutBoxMessageType.PasswordResetRequested),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ChangesPasswordAndRevokesSessions()
    {
        var user = UserFactory.Create();
        var token = CreateToken(user.Id, UserActionTokenType.PasswordReset);
        _tokens.Setup(repository => repository.GetByHashForUpdateAsync(
                TokenHash, UserActionTokenType.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _users.Setup(repository => repository.GetByIdAsync(
                user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyPassword("NewPassword1!", user.PasswordHash))
            .Returns(false);
        _passwordHasher.Setup(hasher => hasher.HashPassword("NewPassword1!"))
            .Returns("new-password-hash");
        var handler = new ResetPasswordHandler(
            _tokens.Object,
            _users.Object,
            _refreshTokens.Object,
            _tokenService.Object,
            _passwordHasher.Object,
            _unitOfWork.Object,
            _audit.Object);

        var result = await handler.Handle(
            new ResetPasswordCommand("raw-token", "NewPassword1!"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("new-password-hash");
        _refreshTokens.Verify(repository => repository.RevokeAllForUserAsync(
            user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(unit => unit.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(repository => repository.AddAsync(
            It.Is<UserAuditEntry>(entry =>
                entry.Action == UserAuditAction.PasswordChanged && entry.ChangesJson == "{}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_WhenNewPasswordMatchesCurrentPassword_PreservesTokenAndSessions()
    {
        var user = UserFactory.Create();
        var originalPasswordHash = user.PasswordHash;
        var token = CreateToken(user.Id, UserActionTokenType.PasswordReset);
        _tokens.Setup(repository => repository.GetByHashForUpdateAsync(
                TokenHash, UserActionTokenType.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _users.Setup(repository => repository.GetByIdAsync(
                user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyPassword("CurrentPassword1!", user.PasswordHash))
            .Returns(true);
        var handler = new ResetPasswordHandler(
            _tokens.Object,
            _users.Object,
            _refreshTokens.Object,
            _tokenService.Object,
            _passwordHasher.Object,
            _unitOfWork.Object,
            _audit.Object);

        var result = await handler.Handle(
            new ResetPasswordCommand("raw-token", "CurrentPassword1!"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("New password must be different from the current password.");
        user.PasswordHash.Should().Be(originalPasswordHash);
        _passwordHasher.Verify(hasher => hasher.HashPassword(It.IsAny<string>()), Times.Never);
        _tokens.Verify(repository => repository.ConsumeActiveForUserAsync(
            It.IsAny<Guid>(),
            It.IsAny<UserActionTokenType>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokens.Verify(repository => repository.RevokeAllForUserAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(repository => repository.AddAsync(
            It.IsAny<UserAuditEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unit => unit.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(unit => unit.RollbackTransactionAsync(CancellationToken.None), Times.Once);
    }

    private static UserActionToken CreateToken(Guid userId, UserActionTokenType type) =>
        UserActionToken.Create(
            userId,
            TokenHash,
            type,
            DateTimeOffset.UtcNow.AddHours(1)).Value!;
}
