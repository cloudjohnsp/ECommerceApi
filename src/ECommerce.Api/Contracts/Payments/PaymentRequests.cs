namespace ECommerce.Api.Contracts.Payments;

public sealed record CreatePaymentRequest(
    Guid OrderId,
    string Currency = "BRL",
    string IdempotencyKey = "");
public sealed record RefundPaymentRequest(string? Reason = null);
