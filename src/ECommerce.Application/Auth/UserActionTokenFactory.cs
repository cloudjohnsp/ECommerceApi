using System.Text.Json;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;

namespace ECommerce.Application.Auth;

internal sealed record PendingUserActionToken(
    UserActionToken Token,
    OutboxMessage OutboxMessage);

internal static class UserActionTokenFactory
{
    internal static Result<PendingUserActionToken> Create(
        User user,
        UserActionTokenType type,
        IUserActionTokenService tokenService)
    {
        var issuedToken = tokenService.Issue();
        var expiresAt = type == UserActionTokenType.EmailConfirmation
            ? DateTimeOffset.UtcNow.AddHours(24)
            : DateTimeOffset.UtcNow.AddHours(1);
        var tokenResult = UserActionToken.Create(
            user.Id,
            issuedToken.TokenHash,
            type,
            expiresAt);
        if (tokenResult.IsFailure)
            return Result<PendingUserActionToken>.Failure([.. tokenResult.Errors]);

        var deliveryType = type == UserActionTokenType.EmailConfirmation
            ? UserEmailDeliveryType.EmailConfirmation
            : UserEmailDeliveryType.PasswordReset;
        var outboxType = type == UserActionTokenType.EmailConfirmation
            ? OutBoxMessageType.EmailConfirmationRequested
            : OutBoxMessageType.PasswordResetRequested;
        var delivery = new UserEmailDelivery(
            user.Email.Value,
            user.FirstName,
            issuedToken.RawToken,
            deliveryType);
        return Result<PendingUserActionToken>.Success(new PendingUserActionToken(
            tokenResult.Value!,
            new OutboxMessage(outboxType, JsonSerializer.Serialize(delivery))));
    }
}
