namespace ECommerce.Application.Abstractions.Payments;

public interface IPaymentWebhookSignatureVerifier
{
    bool IsValid(string payload, string? signature);
}
