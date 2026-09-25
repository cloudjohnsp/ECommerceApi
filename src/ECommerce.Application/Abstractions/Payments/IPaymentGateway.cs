using ECommerce.Shared.Results;

namespace ECommerce.Application.Abstractions.Payments;

public sealed record CreateGatewayPayment(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency);

public sealed record GatewayPayment(string ExternalPaymentId, string Status);

public sealed record RefundGatewayPayment(
    Guid PaymentId,
    string ExternalPaymentId,
    string? Reason);

public interface IPaymentGateway
{
    Task<Result<GatewayPayment>> CreateAsync(
        CreateGatewayPayment payment,
        CancellationToken cancellationToken = default);

    Task<Result<GatewayPayment>> RefundAsync(
        RefundGatewayPayment payment,
        CancellationToken cancellationToken = default);
}
