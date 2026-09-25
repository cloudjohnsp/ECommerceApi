using ECommerce.Application.Payments;
using ECommerce.Application.Payments.Validators;
using FluentAssertions;

namespace ECommerce.Application.Tests.Payments.Validators;

public sealed class PaymentValidatorsTests
{
    [Fact]
    public async Task Refund_WithValidOrderAndReason_IsValid()
    {
        var command = new RefundPaymentCommand(Guid.NewGuid(), "customer_request");

        var result = await new RefundPaymentValidator().ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Refund_WithEmptyOrderOrOversizedReason_IsInvalid()
    {
        var command = new RefundPaymentCommand(Guid.Empty, new string('a', 501));

        var result = await new RefundPaymentValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }
}
