namespace ECommerce.Api.Contracts.Payments;

public sealed record CreatePaymentRequest(Guid OrderId, string Currency = "BRL");
