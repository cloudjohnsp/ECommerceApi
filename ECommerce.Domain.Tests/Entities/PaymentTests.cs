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

        var result = Payment.Create(orderId, 199.90m, "Stripe");

        result.IsSuccess.Should().BeTrue();
        result.Value!.OrderId.Should().Be(orderId);
        result.Value.Amount.Should().Be(199.90m);
        result.Value.Provider.Should().Be("Stripe");
        result.Value.Status.Should().Be(PaymentStatus.Pending);
        result.Value.ExternalPaymentId.Should().BeNull();
        result.Value.PaidAt.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidAmount_ReturnsFailure(decimal amount)
    {
        Payment.Create(Guid.NewGuid(), amount, "Stripe").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarkAsPaid_WithExternalId_CompletesPayment()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "Stripe").Value!;

        var result = payment.MarkAsPaid("pay_123");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Paid);
        payment.ExternalPaymentId.Should().Be("pay_123");
        payment.PaidAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsFailed_AfterPaymentWasPaid_ReturnsFailure()
    {
        var payment = Payment.Create(Guid.NewGuid(), 100m, "Stripe").Value!;
        payment.MarkAsPaid("pay_123");

        var result = payment.MarkAsFailed();

        result.IsFailure.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Paid);
    }
}
