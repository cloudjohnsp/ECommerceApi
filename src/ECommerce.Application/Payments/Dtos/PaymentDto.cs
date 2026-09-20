using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Payments.Dtos;

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    decimal Amount,
    string Provider,
    PaymentStatus Status,
    string? ExternalPaymentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? FailedAt);

internal static class PaymentMapping
{
    internal static PaymentDto ToDto(this Payment payment) => new(
        payment.Id,
        payment.OrderId,
        payment.Amount,
        payment.Provider,
        payment.Status,
        payment.ExternalPaymentId,
        payment.CreatedAt,
        payment.PaidAt,
        payment.FailedAt);
}
