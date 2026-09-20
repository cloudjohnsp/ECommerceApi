using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;

namespace ECommerce.Domain.Entities;

public sealed class Payment : Entity
{
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string? ExternalPaymentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }

    private Payment()
    {
    }

    private Payment(Guid orderId, decimal amount, string provider)
    {
        OrderId = orderId;
        Amount = amount;
        Provider = provider;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static Result<Payment> Create(Guid orderId, decimal amount, string? provider)
    {
        var errors = new List<string>();

        if (orderId == Guid.Empty)
            errors.Add("Order id is required.");
        if (amount <= 0)
            errors.Add("Payment amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(provider) || provider.Trim().Length > 100)
            errors.Add("Payment provider must contain between 1 and 100 characters.");

        return errors.Count > 0
            ? Result<Payment>.Failure([.. errors])
            : Result<Payment>.Success(new Payment(orderId, amount, provider!.Trim()));
    }

    public Result RegisterExternalPayment(string? externalPaymentId)
    {
        if (string.IsNullOrWhiteSpace(externalPaymentId) || externalPaymentId.Trim().Length > 200)
            return Result.Failure("External payment id must contain between 1 and 200 characters.");

        var normalizedExternalPaymentId = externalPaymentId.Trim();
        if (ExternalPaymentId is not null)
            return ExternalPaymentId == normalizedExternalPaymentId
                ? Result.Success()
                : Result.Failure("Payment is already associated with another external payment id.");

        if (Status != PaymentStatus.Pending)
            return Result.Failure("Only pending payments can be associated with an external payment id.");

        ExternalPaymentId = normalizedExternalPaymentId;
        return Result.Success();
    }

    public Result MarkAsPaid(string? externalPaymentId)
    {
        var registerResult = RegisterExternalPayment(externalPaymentId);
        if (registerResult.IsFailure) return registerResult;
        if (Status == PaymentStatus.Paid) return Result.Success();
        if (Status != PaymentStatus.Pending)
            return Result.Failure("Only pending payments can be marked as paid.");

        Status = PaymentStatus.Paid;
        PaidAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result MarkAsFailed()
    {
        if (Status == PaymentStatus.Paid)
            return Result.Failure("A paid payment cannot be marked as failed.");
        if (Status == PaymentStatus.Failed)
            return Result.Success();

        Status = PaymentStatus.Failed;
        FailedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}
