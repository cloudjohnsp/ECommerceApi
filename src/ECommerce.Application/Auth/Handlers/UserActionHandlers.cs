using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using ECommerce.Shared.Results;
using MediatR;
using ECommerce.Application.Users;
using EmailValue = ECommerce.Domain.ValueObjects.Email;

namespace ECommerce.Application.Auth.Handlers;

public sealed class ConfirmEmailHandler(
    IUserActionTokenRepository tokenRepository,
    IUserRepository userRepository,
    IUserActionTokenService tokenService,
    IUnitOfWork unitOfWork,
    IUserAuditRepository auditRepository) : IRequestHandler<ConfirmEmailCommand, Result>
{
    private const string InvalidTokenMessage = "Invalid or expired email confirmation token.";

    public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var token = await tokenRepository.GetByHashAsync(
            tokenService.Hash(request.Token),
            UserActionTokenType.EmailConfirmation,
            cancellationToken);
        if (token is null || !token.IsUsable(now))
            return Result.Failure(InvalidTokenMessage);

        var user = await userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Result.Failure(InvalidTokenMessage);

        user.ConfirmEmail();
        await tokenRepository.ConsumeActiveForUserAsync(
            user.Id,
            UserActionTokenType.EmailConfirmation,
            now,
            cancellationToken);
        await userRepository.UpdateAsync(user, cancellationToken);
        await auditRepository.AddAsync(
            UserAuditEntryFactory.EmailConfirmed(user),
            cancellationToken);
        await unitOfWork.Commit(cancellationToken);
        return Result.Success();
    }
}

public sealed class ForgotPasswordHandler(
    IUserRepository userRepository,
    IUserActionTokenRepository tokenRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUserActionTokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var emailResult = EmailValue.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure([.. emailResult.Errors]);

        var user = await userRepository.GetByEmailAsync(emailResult.Value!.Value, cancellationToken);
        if (user is null || !user.IsActive || !user.IsEmailConfirmed)
            return Result.Success();

        var now = DateTimeOffset.UtcNow;
        await tokenRepository.ConsumeActiveForUserAsync(
            user.Id,
            UserActionTokenType.PasswordReset,
            now,
            cancellationToken);
        var pendingTokenResult = UserActionTokenFactory.Create(
            user,
            UserActionTokenType.PasswordReset,
            tokenService);
        if (pendingTokenResult.IsFailure)
            return Result.Failure([.. pendingTokenResult.Errors]);

        await tokenRepository.AddAsync(pendingTokenResult.Value!.Token, cancellationToken);
        await outboxMessageRepository.AddAsync(
            pendingTokenResult.Value.OutboxMessage,
            cancellationToken);
        await unitOfWork.Commit(cancellationToken);
        return Result.Success();
    }
}

public sealed class ResetPasswordHandler(
    IUserActionTokenRepository tokenRepository,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUserActionTokenService tokenService,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IUserAuditRepository auditRepository) : IRequestHandler<ResetPasswordCommand, Result>
{
    private const string InvalidTokenMessage = "Invalid or expired password reset token.";

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var token = await tokenRepository.GetByHashAsync(
            tokenService.Hash(request.Token),
            UserActionTokenType.PasswordReset,
            cancellationToken);
        if (token is null || !token.IsUsable(now))
            return Result.Failure(InvalidTokenMessage);

        var user = await userRepository.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Result.Failure(InvalidTokenMessage);

        var changeResult = user.ChangePassword(passwordHasher.HashPassword(request.NewPassword));
        if (changeResult.IsFailure)
            return changeResult;

        await tokenRepository.ConsumeActiveForUserAsync(
            user.Id,
            UserActionTokenType.PasswordReset,
            now,
            cancellationToken);
        await refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);
        await userRepository.UpdateAsync(user, cancellationToken);
        await auditRepository.AddAsync(
            UserAuditEntryFactory.PasswordChanged(user, null),
            cancellationToken);
        await unitOfWork.Commit(cancellationToken);
        return Result.Success();
    }
}
