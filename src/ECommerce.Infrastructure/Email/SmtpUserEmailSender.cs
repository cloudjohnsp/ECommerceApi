using ECommerce.Application.Abstractions.Email;
using ECommerce.Infrastructure.Options;
using ECommerce.Shared.Results;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ECommerce.Infrastructure.Email;

public sealed class SmtpUserEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpUserEmailSender> logger) : IUserEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task<Result> SendAsync(
        UserEmailDelivery delivery,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = CreateMessage(delivery);
            using var client = new SmtpClient();
            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(
                    _options.Username,
                    _options.Password ?? string.Empty,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to send {EmailType} e-mail to {Recipient}.",
                delivery.Type,
                delivery.Recipient);
            return Result.Failure("E-mail service is unavailable.");
        }
    }

    private MimeMessage CreateMessage(UserEmailDelivery delivery)
    {
        var isConfirmation = delivery.Type == UserEmailDeliveryType.EmailConfirmation;
        var relativePath = isConfirmation ? "confirm-email" : "reset-password";
        var link = $"{_options.PublicAppBaseUrl.TrimEnd('/')}/{relativePath}?token={Uri.EscapeDataString(delivery.Token)}";
        var action = isConfirmation ? "confirm your e-mail" : "reset your password";
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(delivery.Recipient));
        message.Subject = isConfirmation ? "Confirm your e-mail" : "Reset your password";
        message.Body = new TextPart("plain")
        {
            Text = $"Hello {delivery.FirstName},\n\nUse this link to {action}:\n{link}\n"
        };
        return message;
    }
}
