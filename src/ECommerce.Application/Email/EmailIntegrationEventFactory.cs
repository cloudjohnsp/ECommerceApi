using System.Text.Json;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Email;

internal static class EmailIntegrationEventFactory
{
    public static OutboxMessage Create(Guid deliveryId, UserEmailDeliveryType deliveryType)
    {
        var category = deliveryType switch
        {
            UserEmailDeliveryType.EmailConfirmation => EmailDeliveryCategories.EmailConfirmation,
            UserEmailDeliveryType.PasswordReset => EmailDeliveryCategories.PasswordReset,
            _ => throw new ArgumentOutOfRangeException(
                nameof(deliveryType),
                deliveryType,
                "Unsupported user e-mail delivery type.")
        };
        var payload = new EmailSentIntegrationEventPayload(
            deliveryId,
            category,
            DateTimeOffset.UtcNow);
        return new OutboxMessage(
            OutBoxMessageType.EmailSent,
            JsonSerializer.Serialize(payload));
    }
}
