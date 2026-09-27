using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public sealed class PaymentTests
{
    [Fact]
    public void Create_WithValidData_CreatesPendingPayment()
    {
        var orderId = Guid.NewGuid();

        var result = Payment.Create(orderId, 199.90m, "brl", "Stripe");

        result.IsSuccess.Should().BeTrue();
        result.Value!.OrderId.Should().Be(orderId);
        result.Value.Amount.Should().Be(199.90m);
        result.Value.Currency.Should().Be("BRL");
        result.Value.Provider.Should().Be("Stripe");
        result.Value.Status.Should().Be(PaymentStatus.Pending);
        result.Value.ExternalPaymentId.Should().BeNull();
        result.Value.PaidAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithIdempotencyKey_PersistsNormalizedAttemptIdentity()
    {
        var result = Payment.Create(
            Guid.NewGuid(), 100m, "BRL", "Stripe", "  checkout-attempt-1  ");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IdempotencyKey.Should().Be("checkout-attempt-1");
    }

    [Fact]
    public void Create_WithOversizedIdempotencyKey_ReturnsFailure()
    {
        var result = Payment.Create(
            Guid.NewGuid(),
            100m,
            "BRL",
            "Stripe",
            new string('x', Payment.IdempotencyKeyMaximumLength + 1));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(error => error.Contains("idempotency key"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BR")]
    [InlineData("BRL1")]
    [InlineData("R$L")]
    public void Create_WithInvalidCurrency_ReturnsFailure(string? currency)
    {
        var result = Payment.Create(Guid.NewGuid(), 100m, currency, "Stripe");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Payment currency must be a three-letter ISO code.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidAmount_ReturnsFailure(decimal amount)
    {
        Payment.Create(Guid.NewGuid(), amount, "BRL", "Stripe").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithMaximumPersistableAmount_ReturnsSuccess()
    {
        var result = Payment.Create(Guid.NewGuid(), Order.MaximumTotal, "BRL", "Stripe");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Amount.Should().Be(Order.MaximumTotal);
    }

    [Fact]
    public void Create_WithAmountAboveDatabasePrecision_ReturnsFailure()
    {
        var result = Payment.Create(Guid.NewGuid(), Order.MaximumTotal + 0.01m, "BRL", "Stripe");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain($"Payment amount cannot exceed {Order.MaximumTotal}.");
    }

    [Fact]
    public void Create_WithUnsupportedAmountScale_ReturnsFailure()
    {
        var result = Payment.Create(Guid.NewGuid(), 10.999m, "BRL", "Stripe");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain("Payment amount cannot have more than 2 decimal places.");
    }

    [Fact]
    public void MarkAsPaid_WithExternalId_CompletesPayment()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "BRL", "Stripe").Value!;

        var result = payment.MarkAsPaid("pay_123");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Paid);
        payment.ExternalPaymentId.Should().Be("pay_123");
        payment.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void RegisterExternalPayment_KeepsPaymentPending()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "BRL", "ECommercePayment").Value!;

        var result = payment.RegisterExternalPayment("pay_123");

        result.IsSuccess.Should().BeTrue();
        payment.ExternalPaymentId.Should().Be("pay_123");
        payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void RegisterExternalPayment_WithSameId_IsIdempotent()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "BRL", "ECommercePayment").Value!;
        payment.RegisterExternalPayment("pay_123");

        var result = payment.RegisterExternalPayment("pay_123");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void MarkAsFailed_AfterPaymentWasPaid_ReturnsFailure()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "BRL", "Stripe").Value!;
        payment.MarkAsPaid("pay_123");

        var result = payment.MarkAsFailed();

        result.IsFailure.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void MarkAsRefunded_AfterPaymentWasPaid_ChangesStatusIdempotently()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "BRL", "Stripe").Value!;
        payment.MarkAsPaid("pay_123");

        var first = payment.MarkAsRefunded();
        var second = payment.MarkAsRefunded();

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundedAt.Should().NotBeNull();
        payment.MarkAsFailed().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarkAsRefunded_WhenPaymentIsPending_ReturnsFailure()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "BRL", "Stripe").Value!;

        payment.MarkAsRefunded().IsFailure.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Pending);
    }
}
