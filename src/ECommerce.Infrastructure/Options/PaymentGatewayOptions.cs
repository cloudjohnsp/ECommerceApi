namespace ECommerce.Infrastructure.Options;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    public string BaseUrl { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 10;
}
