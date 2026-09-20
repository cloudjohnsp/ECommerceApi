using ECommerce.Application.Payments.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Payments;

public sealed record CreatePaymentCommand(Guid OrderId, string Currency) : IRequest<Result<PaymentDto>>;
public sealed record ProcessPaymentWebhookCommand(string Payload, string? Signature) : IRequest<Result>;
