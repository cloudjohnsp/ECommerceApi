using ECommerce.Domain.Enums;
using ECommerce.Domain.ValueObjects;
using ECommerce.Shared.Results;

namespace ECommerce.Domain.Entities;

public sealed class Payment : Entity
{
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string? ExternalPaymentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public DateTimeOffset? RefundedAt { get; private set; }

    private Payment()
    {
    }

    private Payment(Guid orderId, decimal amount, string currency, string provider)
    {
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
        Provider = provider;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static Result<Payment> Create(
        Guid orderId,
        decimal amount,
        string? currency,
        string? provider)
    {
        var errors = new List<string>();
        var normalizedCurrency = currency?.Trim().ToUpperInvariant();

        if (orderId == Guid.Empty)
            errors.Add("Order id is required.");
        if (amount <= 0)
            errors.Add("Payment amount must be greater than zero.");
        else
        {
            if (amount > MoneyConstraints.MaximumValue)
                errors.Add($"Payment amount cannot exceed {MoneyConstraints.MaximumValue}.");
            if (!MoneyConstraints.HasSupportedScale(amount))
                errors.Add($"Payment amount cannot have more than {MoneyConstraints.Scale} decimal places.");
        }
        if (normalizedCurrency is null ||
            normalizedCurrency.Length != 3 ||
            normalizedCurrency.Any(character => character is < 'A' or > 'Z'))
        {
            errors.Add("Payment currency must be a three-letter ISO code.");
        }
        if (string.IsNullOrWhiteSpace(provider) || provider.Trim().Length > 100)
            errors.Add("Payment provider must contain between 1 and 100 characters.");

        return errors.Count > 0
            ? Result<Payment>.Failure([.. errors])
            : Result<Payment>.Success(new Payment(
                orderId,
                amount,
                normalizedCurrency!,
                provider!.Trim()));
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
        if (Status == PaymentStatus.Failed)
            return Result.Success();
        if (Status != PaymentStatus.Pending)
            return Result.Failure("Only a pending payment can be marked as failed.");

        Status = PaymentStatus.Failed;
        FailedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result MarkAsRefunded()
    {
        if (Status == PaymentStatus.Refunded)
            return Result.Success();
        if (Status != PaymentStatus.Paid)
            return Result.Failure("Only a paid payment can be refunded.");

        Status = PaymentStatus.Refunded;
        RefundedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}
