namespace ECommerce.Application.Abstractions.Email;

public enum UserEmailDeliveryType
{
    EmailConfirmation = 1,
    PasswordReset = 2
}

public sealed record UserEmailDelivery(
    string Recipient,
    string FirstName,
    string Token,
    UserEmailDeliveryType Type);

public interface IUserEmailSender
{
    Task<ECommerce.Shared.Results.Result> SendAsync(
        UserEmailDelivery delivery,
        CancellationToken cancellationToken = default);
}

public interface IUserEmailOutboxProcessor
{
    Task<ECommerce.Shared.Results.Result> ProcessAsync(
        Guid outboxMessageId,
        CancellationToken cancellationToken = default);
}
